using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VRSchool.Visual;
using static VRSchool.Editor.VRSchoolBuildCore;
using Object = UnityEngine.Object;

namespace VRSchool.Editor
{
    /// <summary>
    /// Hoofdscene Assets/Scenes/Main.unity: het volledige schoolgebouw met de buitenkant uit Entrance_01.
    ///
    /// Plattegrond (wereld, gridlijnen in m; gevel 0,3 m dik op x = ±16, z = 16 en z = 42,2):
    ///   inkomhal (gevel, z 16..19,2) -> instapgang x -2..2, z 19,3..26 -> hoofdgang (oost-west) z 26..30,2
    ///   voorkant (diepte 10): kantoor | klas A | instapgang | klas B | leraarskamer
    ///   achterkant (diepte 12): refter | toiletten | bibliotheek | computerlokaal
    /// Elke deur in de gangen hoort bij een lokaal. Lokalen staan 0,2 m uit elkaar (twee muren rug aan rug);
    /// waar een lokaal tegen de gevel ligt, is de gevel (baksteen met ramen) zijn buitenmuur.
    /// Klas A/B en de refter komen uit Classroom_01 / Cafeteria_01 (hun muren aan de gevelkant worden weggehaald),
    /// de andere lokalen bouwt dit script met de bestaande prefabs.
    /// Vereist dat Fase 1-5 en de graffiti gebouwd zijn. Opnieuw uitvoeren is veilig (de bronscenes blijven ongewijzigd).
    /// </summary>
    public static class VRSchoolMainSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string EntrancePath = "Assets/Scenes/Entrance_01.unity";
        const string ClassroomPath = "Assets/Scenes/Classroom_01.unity";
        const string CafeteriaPath = "Assets/Scenes/Cafeteria_01.unity";
        const string ConcretePath = "Assets/Art/Materials/M_Concrete.mat";

        // Gebouw
        const float FrontZ = 16f, BackZ = 42.2f, SideX = 16f, FacadeHeight = 6.4f;
        const float LobbyBackZ = 19.2f;                    // achterkant inkomhal (gevel-gridlijn + 0,15 + 3 + 0,05)
        const float EntryWallZ = LobbyBackZ + 0.1f;        // muur met de dubbele deur tussen inkomhal en instapgang
        const float HallSouthZ = 26f, HallNorthZ = 30.2f;  // gridlijnen van de hoofdgang
        const float EntryX = 2.1f;                         // gridlijnen van de instapgang (±)
        const float Face = 0.1f;                           // halve muurdikte
        const float Ceiling = 3f;

        [Flags] enum Side { None = 0, N = 1, S = 2, W = 4, E = 8 }

        struct RoomRect
        {
            public float x0, z0, x1, z1;
            public RoomRect(float x0, float z0, float x1, float z1) { this.x0 = x0; this.z0 = z0; this.x1 = x1; this.z1 = z1; }
            public float Cx => (x0 + x1) / 2f;
            public float Cz => (z0 + z1) / 2f;
        }

        [MenuItem("VR School/Build Main Scene")]
        public static void BuildAllMenu() => Debug.Log(BuildAll());

        public static string BuildAll()
        {
            var log = new StringBuilder();
            foreach (var path in new[] { EntrancePath, ClassroomPath, CafeteriaPath })
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    return $"!! {path} ontbreekt: bouw eerst die fase (menu VR School).";
            if (SceneManager.GetActiveScene().path == ScenePath)
                return $"!! {ScenePath} is open: open eerst een andere scene en voer dit opnieuw uit.";

            var previousActive = SceneManager.GetActiveScene();
            var main = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(main);
                var environment = Root("Environment");
                var outdoor = Child(environment, "Outdoor");
                var school = Root("SchoolBuilding");
                var shell = Child(school, "Shell");
                var corridors = Child(school, "Corridors");
                var rooms = Child(school, "Rooms");
                var lighting = Root("Lighting");
                var player = Root("Player");
                var systems = Root("Systems");

                EnsureBlankFacadePrefab(log);
                ImportEntrance(main, outdoor, shell, lighting, player, log);
                BuildShell(shell, log);
                FixGround(outdoor, log);
                BuildCorridors(corridors, log);
                BuildRooms(rooms, log);
                PlaceGraffiti(Child(environment, "Graffiti"), log);
                AddInteriorZone(systems, log);

                EditorSceneManager.SaveScene(main, ScenePath);
                AddToBuildSettings(log);
                log.Insert(0, $"Scene opgeslagen: {ScenePath}\n");
            }
            finally
            {
                if (previousActive.IsValid())
                    SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(main, true);
            }
            return log.ToString();
        }

        // ------------------------------------------------------------------ buitenkant

        /// <summary>Haalt de buitenkant uit Entrance_01 (zonder showcase) en hangt alles in de nieuwe structuur.</summary>
        static void ImportEntrance(Scene main, Transform outdoor, Transform shell, Transform lighting, Transform player, StringBuilder log)
        {
            var scene = EditorSceneManager.OpenScene(EntrancePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var fog = (RenderSettings.fog, RenderSettings.fogMode, RenderSettings.fogColor,
                       RenderSettings.fogStartDistance, RenderSettings.fogEndDistance);
            var site = scene.GetRootGameObjects().First(g => g.name == "Entrance_01").transform;
            var rig = scene.GetRootGameObjects().First(g => g.GetComponent<Unity.XR.CoreUtils.XROrigin>() != null);
            foreach (var go in new[] { site.gameObject, rig })
                SceneManager.MoveGameObjectToScene(go, main);
            EditorSceneManager.CloseScene(scene, true);

            SceneManager.SetActiveScene(main);
            (RenderSettings.fog, RenderSettings.fogMode, RenderSettings.fogColor,
             RenderSettings.fogStartDistance, RenderSettings.fogEndDistance) = fog;

            rig.transform.SetParent(player, true);
            foreach (var name in new[] { "Ground", "Street", "Greenery" })
                site.Find(name).SetParent(outdoor, true);
            foreach (Transform light in site.Find("Lighting").Cast<Transform>().ToArray())
                light.SetParent(lighting, true);
            var building = site.Find("Building");
            foreach (Transform piece in building.Cast<Transform>().ToArray())
                piece.SetParent(shell, true);
            Object.DestroyImmediate(site.gameObject);

            // De korte zijgevels (8 m) van de ingangsscene maken plaats voor de volledige zijgevels.
            var removed = 0;
            foreach (var piece in shell.Cast<Transform>().Where(t => t.name == "SM_Out_Facade_4m" && Mathf.Abs(Center(t).x) > SideX - 1f).ToArray())
            {
                Object.DestroyImmediate(piece.gameObject);
                removed++;
            }
            // Gevelmodule links van de ingang zonder raam op het gelijkvloers: muur voor de controls-graffiti.
            var left = shell.Cast<Transform>().First(t => t.name == "SM_Out_Facade_4m" && Mathf.Abs(Center(t).x - GraffitiModuleX) < 1f);
            var blank = Spawn(BlankFacade, shell, left.position, left.rotation);
            blank.transform.SetSiblingIndex(left.GetSiblingIndex());
            Object.DestroyImmediate(left.gameObject);
            log.AppendLine($"Buitenkant uit Entrance_01 overgenomen ({removed} korte zijgevelstukken vervangen, blinde module op x = {GraffitiModuleX})");
        }

        const string BlankFacade = "SM_Out_Facade_4m_Blank";
        const float GraffitiModuleX = -6f;   // module x -8..-4, direct links van de ingang

        static void EnsureBlankFacadePrefab(StringBuilder log)
        {
            var defs = new[] { new AssetDef(BlankFacade, "Outdoor", Kind.Static) };
            RemapModelMaterials(defs, log);
            DetectAxes(log);
            BuildPrefabs(defs, log);
        }

        static void BuildShell(Transform shell, StringBuilder log)
        {
            var depth = BackZ - FrontZ;
            var sideModules = Mathf.RoundToInt(depth / 4f);
            FacadeRun(shell, new Vector3(-SideX, 0f, FrontZ), Vector3.forward, Vector3.left, sideModules, depth / sideModules);
            FacadeRun(shell, new Vector3(SideX, 0f, FrontZ), Vector3.forward, Vector3.right, sideModules, depth / sideModules);
            FacadeRun(shell, new Vector3(-SideX, 0f, BackZ), Vector3.right, Vector3.forward, 8, 4f);
            foreach (var x in new[] { -SideX, SideX })
                Spawn("SM_Out_Facade_Corner", shell, new Vector3(x, 0f, BackZ), Quaternion.identity);

            // Plat dak binnen de dakrand
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            Object.DestroyImmediate(roof.GetComponent<Collider>());
            roof.transform.SetParent(shell, false);
            roof.transform.position = new Vector3(0f, FacadeHeight - 0.1f, (FrontZ + BackZ) / 2f);
            roof.transform.localScale = new Vector3(2f * SideX, 0.2f, depth);
            roof.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ConcretePath);
            GameObjectUtility.SetStaticEditorFlags(roof, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            log.AppendLine($"Gebouw: {2 * SideX} x {depth:0.0} m, zijgevels {sideModules} x {depth / sideModules:0.00} m, achtergevel 8 x 4 m, dak");
        }

        /// <summary>Gevelmodules (4 m, buitenkant = Blender -Y) achter elkaar; geschaald als de lengte geen 4 m is.</summary>
        static void FacadeRun(Transform parent, Vector3 start, Vector3 dir, Vector3 outward, int count, float length)
        {
            var rot = Facing(outward);
            var along = rot * B2U(1, 0, 0);
            for (var i = 0; i < count; i++)
            {
                var a = start + dir * (i * length);
                var pivot = Vector3.Dot(along, dir) > 0f ? a : a + dir * length;
                Spawn("SM_Out_Facade_4m", parent, pivot, rot).transform.localScale = new Vector3(length / 4f, 1f, 1f);
            }
        }

        /// <summary>
        /// Gras en vloeren lagen allebei op y = 0 (z-fighting binnen). Gras 2 cm omlaag; de bestrating (bovenkant 0,02)
        /// en de binnenvloeren blijven erboven. Extra rij gras achter het gebouw.
        /// </summary>
        static void FixGround(Transform outdoor, StringBuilder log)
        {
            var ground = outdoor.Find("Ground");
            var grass = ground.Cast<Transform>().Where(t => t.name == "SM_Out_Ground_Grass_20x20").ToList();
            var corner = B2U(1f, 1f, 0f);
            foreach (var x0 in new[] { -20f, 0f })
                grass.Add(Spawn("SM_Out_Ground_Grass_20x20", ground,
                    new Vector3(x0 + (corner.x < 0 ? 20f : 0f), 0f, 28f + (corner.z < 0 ? 20f : 0f)), Quaternion.identity).transform);
            foreach (var g in grass)
                g.position = new Vector3(g.position.x, -0.02f, g.position.z);
            log.AppendLine($"Grond: {grass.Count} grasvelden 2 cm verlaagd (geen z-fighting meer met de vloeren)");
        }

        // ------------------------------------------------------------------ gangen

        static void BuildCorridors(Transform corridors, StringBuilder log)
        {
            // --- Instapgang: muur met dubbele deur achter de inkomhal; de zijmuren zijn de muren van klas A en B.
            var entry = Child(corridors, "EntryCorridor");
            var walls = Child(entry, "Walls");
            var props = Child(entry, "Props");
            var door = Run(walls, new Vector3(-2f, 0f, EntryWallZ), Vector3.right, Vector3.forward, "SM_Build_Wall_Door_Double")[0];
            SpawnLocal("SM_Build_Door_Double", walls, door, 1.15f, 0f, 0f);
            SpawnLocal("SM_Build_ExitSign", props, door, 2.0f, -0.1f, 2.35f);
            FloorCeiling(entry, -EntryX, EntryWallZ, EntryX, HallSouthZ);
            foreach (var z in new[] { 21.2f, 24.4f })
                Spawn("SM_Build_Light_TL", Child(entry, "Ceiling"), new Vector3(0f, Ceiling, z), Quaternion.identity);
            var wallW = -EntryX + Face;
            var wallE = EntryX - Face;
            Spawn("SM_Hall_Bench", props, new Vector3(wallW + 0.3f, 0f, 22.6f), Facing(Vector3.right));
            Spawn("SM_Hall_PictureFrame", props, new Vector3(wallW, 1.6f, 24.6f), Facing(Vector3.right));
            Spawn("SM_Hall_InfoScreen", props, new Vector3(wallE, 1.7f, 22.6f), Facing(Vector3.left));
            foreach (var x in new[] { -1.55f, 1.55f })
            {
                Spawn("SM_Hall_Planter", props, new Vector3(x, 0f, EntryWallZ + 0.45f), Quaternion.identity);
                Spawn("SM_Hall_Plant", props, new Vector3(x, 0.40f, EntryWallZ + 0.45f), Quaternion.Euler(0f, x * 70f, 0f));
            }

            // --- Hoofdgang oost-west: beide zijden zijn deurmuren van de lokalen.
            var hall = Child(corridors, "MainCorridor");
            props = Child(hall, "Props");
            var lockers = Child(hall, "Lockers");
            FloorCeiling(hall, -SideX, HallSouthZ, SideX, HallNorthZ);
            var ceiling = Child(hall, "Ceiling");
            var midZ = (HallSouthZ + HallNorthZ) / 2f;
            for (var x = -14f; x <= 14f; x += 4f)
                Spawn("SM_Build_Light_TL", ceiling, new Vector3(x, Ceiling, midZ), Quaternion.identity);
            foreach (var x in new[] { -8f, 8f })
                Spawn("SM_Build_SmokeDetector", ceiling, new Vector3(x, Ceiling, midZ), Quaternion.identity);

            // Zuidzijde (kijkt naar het noorden): lockers in de vrije stukken tussen de deuren
            var south = HallSouthZ + Face;
            var toNorth = Facing(Vector3.forward);
            var count = 0;
            foreach (var (a, b) in new[] { (-15.75f, -12.8f), (-7.8f, -2.4f), (4.4f, 10.2f), (12.8f, 15.75f) })
                for (var x = a + 0.75f; x + 0.75f <= b; x += 1.5f, count++)
                    Spawn("SM_Hall_Locker_Row_5", lockers, new Vector3(x, 0f, south), toNorth);

            // Noordzijde (kijkt naar het zuiden): automaat + fontein bij de refter, vitrine recht tegenover de instapgang
            var north = HallNorthZ - Face;
            var toSouth = Facing(Vector3.back);
            Spawn("SM_Hall_VendingMachine", props, new Vector3(-9.1f, 0f, north), toSouth);
            Spawn("SM_Hall_WaterFountain", props, new Vector3(-8.2f, 0f, north), toSouth);
            Spawn("SM_Hall_Bench", props, new Vector3(-6.2f, 0f, north - 0.3f), toSouth);
            Spawn("SM_Hall_TrophyCase", props, new Vector3(0f, 0f, north), toSouth);
            Spawn("SM_Hall_PictureFrame", props, new Vector3(-1.4f, 1.6f, north), toSouth);
            Spawn("SM_Hall_PictureFrame", props, new Vector3(1.4f, 1.6f, north), toSouth);
            Spawn("SM_Hall_Bench", props, new Vector3(3.3f, 0f, north - 0.3f), toSouth);
            foreach (var x in new[] { 11.8f, 13.3f })
                Spawn("SM_Hall_Locker_Row_5", lockers, new Vector3(x, 0f, north), toSouth);
            foreach (var x in new[] { -15.4f, 15.4f })
            {
                Spawn("SM_Hall_Planter", props, new Vector3(x, 0f, midZ), Quaternion.identity);
                Spawn("SM_Hall_Plant", props, new Vector3(x, 0.40f, midZ), Quaternion.Euler(0f, x * 11f, 0f));
            }
            Spawn("SM_Build_FireExtinguisher", props, new Vector3(-6.8f, 0f, south + 0.15f), Quaternion.identity);
            Spawn("SM_Build_FireExtinguisher", props, new Vector3(7.2f, 0f, north - 0.15f), Quaternion.identity);
            log.AppendLine($"Gangen: instapgang {HallSouthZ - EntryWallZ:0.0} m, hoofdgang {2 * SideX} m met {count + 2} lockerblokken");
        }

        // ------------------------------------------------------------------ lokalen

        static void BuildRooms(Transform rooms, StringBuilder log)
        {
            // Voorkant (deur in de zuidmuur van de hoofdgang, z = 26)
            var classA = ImportRoom(rooms, ClassroomPath, "Classroom_01", "Classroom_A", new Vector3(-EntryX, 0f, FrontZ), -90f);
            var classB = ImportRoom(rooms, ClassroomPath, "Classroom_01", "Classroom_B", new Vector3(10.1f, 0f, FrontZ), -90f);
            BuildOffice(rooms, new RoomRect(-SideX, FrontZ, -10.3f, HallSouthZ));
            BuildTeachersRoom(rooms, new RoomRect(10.3f, FrontZ, SideX, HallSouthZ));

            // Achterkant (deur in de noordmuur van de hoofdgang, z = 30,2)
            var cafeteria = ImportRoom(rooms, CafeteriaPath, "Cafeteria_01", "Cafeteria", new Vector3(-SideX, 0f, BackZ), 90f);
            BuildBathroom(rooms, new RoomRect(-5.8f, HallNorthZ, -0.2f, BackZ));
            BuildLibrary(rooms, new RoomRect(0f, HallNorthZ, 8f, BackZ));
            BuildComputerLab(rooms, new RoomRect(8.2f, HallNorthZ, SideX, BackZ));

            var removed = new[] { classA, classB, cafeteria }.Sum(RemoveFacadeSide);
            log.AppendLine($"Lokalen: {rooms.childCount} ({string.Join(", ", rooms.Cast<Transform>().Select(t => t.name))}); " +
                           $"{removed} muurstukken/ramen/radiatoren aan de gevelkant van de geïmporteerde lokalen verwijderd");
        }

        static Transform ImportRoom(Transform rooms, string scenePath, string rootName, string newName, Vector3 origin, float yaw)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var root = scene.GetRootGameObjects().First(g => g.name == rootName);
            SceneManager.MoveGameObjectToScene(root, rooms.gameObject.scene);
            EditorSceneManager.CloseScene(scene, true);
            root.name = newName;
            root.transform.SetParent(rooms, false);
            root.transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, yaw, 0f));
            return root.transform;
        }

        /// <summary>Waar een geïmporteerd lokaal tegen de gevel ligt, is de gevel de buitenmuur: eigen muur, ramen en radiatoren weg.</summary>
        static int RemoveFacadeSide(Transform room)
        {
            var doomed = room.GetComponentsInChildren<Transform>(true)
                .Where(t => t.parent != null && t.parent.parent == room)
                .Where(t => t.name.StartsWith("SM_Build_Wall") || t.name == "SM_Build_Window_Glass" || t.name == "SM_Build_Radiator")
                .Where(t => NearFacade(Center(t)))
                .ToArray();
            foreach (var t in doomed)
                Object.DestroyImmediate(t.gameObject);
            return doomed.Length;
        }

        static bool NearFacade(Vector3 p) =>
            Mathf.Abs(p.x + SideX) < 0.35f || Mathf.Abs(p.x - SideX) < 0.35f ||
            Mathf.Abs(p.z - FrontZ) < 0.35f || Mathf.Abs(p.z - BackZ) < 0.35f;

        /// <summary>Kaal lokaal: vloer, plafond, muren aan de niet-gevelkanten, één klasdeur, hoeken en TL's.</summary>
        static Transform CustomRoom(Transform rooms, string name, RoomRect r, Side facades, Side doorSide, float doorAt,
            out Transform furniture, out Transform props)
        {
            var room = Child(rooms, name);
            var walls = Child(room, "Walls");
            furniture = Child(room, "Furniture");
            props = Child(room, "Props");
            FloorCeiling(room, r.x0, r.z0, r.x1, r.z1);

            void Side_(Side side, Vector3 a, Vector3 b, Vector3 inward, float along)
            {
                if ((facades & side) != 0)
                    return;
                if (side == doorSide)
                    WallWithDoor(walls, room.Find("Props"), a, b, inward, along);
                else
                    PlainWall(walls, a, b, inward);
            }
            Side_(Side.S, new Vector3(r.x0, 0, r.z0), new Vector3(r.x1, 0, r.z0), Vector3.forward, doorAt - r.x0);
            Side_(Side.N, new Vector3(r.x0, 0, r.z1), new Vector3(r.x1, 0, r.z1), Vector3.back, doorAt - r.x0);
            Side_(Side.W, new Vector3(r.x0, 0, r.z0), new Vector3(r.x0, 0, r.z1), Vector3.right, doorAt - r.z0);
            Side_(Side.E, new Vector3(r.x1, 0, r.z0), new Vector3(r.x1, 0, r.z1), Vector3.left, doorAt - r.z0);

            foreach (var (x, z, a, b) in new[] { (r.x0, r.z0, Side.W, Side.S), (r.x1, r.z0, Side.E, Side.S),
                                                (r.x0, r.z1, Side.W, Side.N), (r.x1, r.z1, Side.E, Side.N) })
                if ((facades & (a | b)) == 0)
                    Spawn("SM_Build_Wall_Corner_In", walls, new Vector3(x, 0f, z), Quaternion.identity);

            // TL's in een raster van ongeveer 3 m
            var ceiling = room.Find("Ceiling");
            var nx = Mathf.Max(1, Mathf.RoundToInt((r.x1 - r.x0) / 3f));
            var nz = Mathf.Max(1, Mathf.RoundToInt((r.z1 - r.z0) / 3.5f));
            for (var i = 0; i < nx; i++)
            for (var j = 0; j < nz; j++)
                Spawn("SM_Build_Light_TL", ceiling,
                    new Vector3(Mathf.Lerp(r.x0, r.x1, (i + 0.5f) / nx), Ceiling, Mathf.Lerp(r.z0, r.z1, (j + 0.5f) / nz)), Quaternion.identity);
            Spawn("SM_Build_SmokeDetector", ceiling, new Vector3(r.Cx + 0.6f, Ceiling, r.Cz), Quaternion.identity);
            return room;
        }

        static void BuildOffice(Transform rooms, RoomRect r)
        {
            // Secretariaat: twee werkplekken met de rug naar het raam, kasten tegen de oostmuur.
            CustomRoom(rooms, "Office", r, Side.W | Side.S, Side.N, -11.5f, out var furniture, out var props);
            float xe = r.x1 - Face, zn = r.z1 - Face;
            foreach (var z in new[] { 19f, 23f })
            {
                Spawn("SM_Class_TeacherDesk", furniture, new Vector3(-13.5f, 0f, z), Facing(Vector3.back));
                Spawn("SM_Class_OfficeChair", furniture, new Vector3(-13.3f, 0f, z - 0.65f), Facing(Vector3.forward));
                Spawn("SM_Class_PC_Monitor", props, new Vector3(-13.85f, 0.75f, z + 0.15f), Facing(Vector3.back));
                Spawn("SM_Class_Keyboard", props, new Vector3(-13.85f, 0.75f, z - 0.18f), Facing(Vector3.back));
                foreach (var dx in new[] { -0.5f, 0.5f })
                    Spawn("SM_Class_Chair_A", furniture, new Vector3(-13.5f + dx, 0f, z + 0.8f), Facing(Vector3.back));
            }
            Spawn("SM_Class_Laptop_Closed", props, new Vector3(-13f, 0.75f, 23.1f), Facing(Vector3.back) * Quaternion.Euler(0f, 20f, 0f));
            Spawn("SM_Class_Cabinet", furniture, new Vector3(xe - 0.25f, 0f, 17.2f), Facing(Vector3.left));
            Spawn("SM_Class_Bookshelf", furniture, new Vector3(xe - 0.17f, 0f, 18.4f), Facing(Vector3.left));
            Spawn("SM_Class_Bookshelf", furniture, new Vector3(xe - 0.17f, 0f, 19.35f), Facing(Vector3.left));
            Spawn("SM_Class_Cabinet", furniture, new Vector3(xe - 0.25f, 0f, 21.6f), Facing(Vector3.left));
            Spawn("SM_Class_WallClock", props, new Vector3(xe, 2.3f, 23.4f), Facing(Vector3.left));
            Spawn("SM_Class_Poster_Frame", props, new Vector3(xe, 1.55f, 24.6f), Facing(Vector3.left));
            Spawn("SM_Class_Corkboard", props, new Vector3(-14.2f, 1.5f, zn), Facing(Vector3.back));
            Spawn("SM_Class_CoatRack", props, new Vector3(-15.15f, 1.65f, zn - 0.6f), Facing(Vector3.right));
            Spawn("SM_Class_TrashBin", props, new Vector3(-14.5f, 0f, 18.3f), Quaternion.identity);
            Planter(furniture, new Vector3(-15.4f, 0f, 25.3f));
        }

        static void BuildTeachersRoom(Transform rooms, RoomRect r)
        {
            // Leraarskamer: twee lange tafels, lockers en een keukenhoek met microgolf.
            CustomRoom(rooms, "TeachersRoom", r, Side.E | Side.S, Side.N, 11.5f, out var furniture, out var props);
            float xw = r.x0 + Face, zn = r.z1 - Face;
            var rng = new System.Random(21);
            foreach (var z in new[] { 19.2f, 22.4f })
            {
                Spawn("SM_Cafe_LongTable", furniture, new Vector3(13.4f, 0f, z), Quaternion.identity);
                foreach (var side in new[] { -1f, 1f })
                foreach (var dx in new[] { -0.8f, 0f, 0.8f })
                    Spawn("SM_Cafe_Chair", furniture, new Vector3(13.4f + dx, 0f, z + side * 0.6f), Facing(new Vector3(0f, 0f, -side)));
                foreach (var (prefab, dx) in new[] { ("SM_Prop_WaterBottle", -0.7f), ("SM_Prop_LunchBox", 0.2f), ("SM_Prop_Notebook", 0.9f) })
                    Spawn(prefab, props, new Vector3(13.4f + dx, 0.75f, z + (float)rng.NextDouble() * 0.3f - 0.15f),
                        Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
            }
            Spawn("SM_Hall_Locker_Row_5", furniture, new Vector3(xw, 0f, 17.4f), Facing(Vector3.right));
            foreach (var z in new[] { 20.1f, 21.1f })
                Spawn("SM_Class_Cabinet", furniture, new Vector3(xw + 0.25f, 0f, z), Facing(Vector3.right));
            Spawn("SM_Cafe_Microwave", props, new Vector3(xw + 0.25f, 1.0f, 20.1f), Facing(Vector3.right));
            Spawn("SM_Class_Bookshelf", furniture, new Vector3(xw + 0.17f, 0f, 23.4f), Facing(Vector3.right));
            Spawn("SM_Class_WallClock", props, new Vector3(xw, 2.3f, 22.3f), Facing(Vector3.right));
            Spawn("SM_Class_Corkboard", props, new Vector3(14.1f, 1.5f, zn), Facing(Vector3.back));
            Spawn("SM_Class_CoatRack", props, new Vector3(15.25f, 1.65f, zn - 0.6f), Facing(Vector3.left));
            Spawn("SM_Class_TrashBin", props, new Vector3(xw + 0.35f, 0f, 24.6f), Quaternion.identity);
            Planter(furniture, new Vector3(15.4f, 0f, 16.6f));
        }

        static void BuildBathroom(Transform rooms, RoomRect r)
        {
            // Toiletten: cabines langs beide zijmuren, lavabo's met spiegels vooraan rechts.
            var room = CustomRoom(rooms, "Bathroom", r, Side.N, Side.S, -3f, out var furniture, out var props);
            float xw = r.x0 + Face, xe = r.x1 - Face;
            for (var i = 0; i < 6; i++)
            {
                var p = new Vector3(xw, 0f, 36.1f + i * 0.9f);
                Spawn("SM_WC_Stall", furniture, p, Facing(Vector3.right));
                Spawn("SM_WC_Toilet", furniture, p, Facing(Vector3.right));
            }
            for (var i = 0; i < 4; i++)
            {
                var p = new Vector3(xe, 0f, 38.0f + i * 0.9f);
                Spawn("SM_WC_Stall", furniture, p, Facing(Vector3.left));
                Spawn("SM_WC_Toilet", furniture, p, Facing(Vector3.left));
            }
            foreach (var z in new[] { 31.5f, 32.4f, 33.3f, 34.2f })
            {
                Spawn("SM_WC_Sink", furniture, new Vector3(xe, 0f, z), Facing(Vector3.left));
                Spawn("SM_WC_Mirror", props, new Vector3(xe, 1.45f, z), Facing(Vector3.left));
                Spawn("SM_WC_SoapDispenser", props, new Vector3(xe, 1.1f, z + 0.45f), Facing(Vector3.left));
            }
            Spawn("SM_WC_HandDryer", props, new Vector3(xe, 1.2f, 35.3f), Facing(Vector3.left));
            Spawn("SM_Class_TrashBin", props, new Vector3(xe - 0.3f, 0f, 35.9f), Quaternion.identity);
            Spawn("SM_WC_HandDryer", props, new Vector3(xw, 1.2f, 32.5f), Facing(Vector3.right));

            var probe = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(room, false);
            probe.transform.position = new Vector3(r.Cx, 1.5f, r.Cz);
            probe.mode = ReflectionProbeMode.Baked;
            probe.size = new Vector3(r.x1 - r.x0, Ceiling, r.z1 - r.z0);
            probe.boxProjection = true;
        }

        static void BuildLibrary(Transform rooms, RoomRect r)
        {
            // Bibliotheek: boekenrekken langs beide zijmuren, twee leestafels met boeken.
            CustomRoom(rooms, "Library", r, Side.N, Side.S, 6f, out var furniture, out var props);
            float xw = r.x0 + Face, xe = r.x1 - Face, zs = r.z0 + Face;
            for (var z = 31.0f; z <= 40.6f; z += 0.95f)
                Spawn("SM_Class_Bookshelf", furniture, new Vector3(xw + 0.17f, 0f, z), Facing(Vector3.right));
            for (var z = 32.4f; z <= 40.6f; z += 0.95f)
                Spawn("SM_Class_Bookshelf", furniture, new Vector3(xe - 0.17f, 0f, z), Facing(Vector3.left));
            var rng = new System.Random(3);
            var books = new[] { "SM_Prop_Book_A", "SM_Prop_Book_B", "SM_Prop_Book_C", "SM_Prop_Notebook" };
            foreach (var z in new[] { 34.6f, 38.4f })
            {
                Spawn("SM_Cafe_LongTable", furniture, new Vector3(4f, 0f, z), Quaternion.identity);
                foreach (var side in new[] { -1f, 1f })
                foreach (var dx in new[] { -0.8f, 0f, 0.8f })
                    Spawn("SM_Cafe_Chair", furniture, new Vector3(4f + dx, 0f, z + side * 0.6f), Facing(new Vector3(0f, 0f, -side)));
                for (var i = 0; i < 4; i++)
                    Spawn(books[rng.Next(books.Length)], props,
                        new Vector3(4f + (float)rng.NextDouble() * 2f - 1f, 0.76f, z + (float)rng.NextDouble() * 0.4f - 0.2f),
                        Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
            }
            Spawn("SM_Class_WorldMap", props, new Vector3(2.5f, 1.65f, zs), Facing(Vector3.forward));
            Spawn("SM_Class_WallClock", props, new Vector3(4.2f, 2.45f, zs), Facing(Vector3.forward));
            foreach (var x in new[] { 1.5f, 6.5f })
                Planter(furniture, new Vector3(x, 0f, 41.5f));
        }

        static void BuildComputerLab(Transform rooms, RoomRect r)
        {
            // Computerlokaal: smartboard op de westmuur, 3 kolommen pc-tafels die ernaar kijken.
            CustomRoom(rooms, "ComputerLab", r, Side.N | Side.E, Side.S, 9.5f, out var furniture, out var props);
            var xw = r.x0 + Face;
            Spawn("SM_Class_SmartBoard", props, new Vector3(xw, 0.9f, 36.4f), Facing(Vector3.right));
            Spawn("SM_Class_TeacherDesk", furniture, new Vector3(9.8f, 0f, 36.4f), Facing(Vector3.left));
            Spawn("SM_Class_OfficeChair", furniture, new Vector3(9.15f, 0f, 36.6f), Facing(Vector3.right));
            Spawn("SM_Class_PC_Monitor", props, new Vector3(9.95f, 0.75f, 36.2f), Facing(Vector3.left));
            Spawn("SM_Class_Keyboard", props, new Vector3(9.62f, 0.75f, 36.2f), Facing(Vector3.left));
            foreach (var x in new[] { 11.6f, 13.2f, 14.8f })
            foreach (var z in new[] { 31.9f, 32.65f, 34.5f, 35.25f, 37.4f, 38.15f, 40.0f, 40.75f })
            {
                Spawn("SM_Class_Desk_A", furniture, new Vector3(x, 0f, z), Facing(Vector3.right));
                Spawn("SM_Class_Chair_A", furniture, new Vector3(x + 0.42f, 0f, z), Facing(Vector3.left));
                Spawn("SM_Class_PC_Monitor", props, new Vector3(x - 0.1f, 0.76f, z), Facing(Vector3.right));
                Spawn("SM_Class_Keyboard", props, new Vector3(x + 0.13f, 0.76f, z), Facing(Vector3.right));
            }
            Spawn("SM_Class_Cabinet", furniture, new Vector3(xw + 0.25f, 0f, 40.9f), Facing(Vector3.right));
            Spawn("SM_Class_TrashBin", props, new Vector3(xw + 0.3f, 0f, 31.0f), Quaternion.identity);
            Spawn("SM_Class_WallClock", props, new Vector3(xw, 2.45f, 33.6f), Facing(Vector3.right));
        }

        static void Planter(Transform parent, Vector3 p)
        {
            Spawn("SM_Hall_Planter", parent, p, Quaternion.identity);
            Spawn("SM_Hall_Plant", parent, p + Vector3.up * 0.40f, Quaternion.Euler(0f, p.x * 37f % 360f, 0f));
        }

        // ------------------------------------------------------------------ muren, vloeren

        /// <summary>Gladde muur van a naar b (een geschaald 4 m-stuk); de kamerzijde kijkt naar inward.</summary>
        static void PlainWall(Transform parent, Vector3 a, Vector3 b, Vector3 inward)
        {
            var length = Vector3.Distance(a, b);
            if (length < 0.05f)
                return;
            var dir = (b - a) / length;
            var rot = Facing(inward);
            var pivot = Vector3.Dot(rot * B2U(1, 0, 0), dir) > 0f ? a : b;
            Spawn("SM_Build_Wall_4m", parent, pivot, rot).transform.localScale = new Vector3(length / 4f, 1f, 1f);
        }

        /// <summary>Muur van a naar b met een klasdeur (2 m-stuk) waarvan het midden op 'doorAt' meter vanaf a ligt.</summary>
        static void WallWithDoor(Transform walls, Transform props, Vector3 a, Vector3 b, Vector3 inward, float doorAt)
        {
            var dir = (b - a).normalized;
            var d0 = a + dir * (doorAt - 1f);
            PlainWall(walls, a, d0, inward);
            var door = Run(walls, d0, dir, inward, "SM_Build_Wall_Door")[0];
            SpawnLocal("SM_Build_Door_Classroom", walls, door, 0.555f, 0f, 0f);
            SpawnLocal("SM_Build_LightSwitch", props, door, 1.75f, -0.1f, 1.1f);
            PlainWall(walls, d0 + dir * 2f, b, inward);
        }

        /// <summary>Vloer- en plafondtegels (2 x 2 m) over [x0,x1] x [z0,z1]; de laatste rij/kolom wordt geschaald.</summary>
        static void FloorCeiling(Transform room, float x0, float z0, float x1, float z1)
        {
            var floor = Child(room, "Floor");
            var ceiling = Child(room, "Ceiling");
            var corner = B2U(1f, 1f, 0f);
            for (var x = x0; x < x1 - 0.01f; x += 2f)
            for (var z = z0; z < z1 - 0.01f; z += 2f)
            {
                var w = Mathf.Min(2f, x1 - x);
                var d = Mathf.Min(2f, z1 - z);
                var p = new Vector3(corner.x < 0 ? x + w : x, 0f, corner.z < 0 ? z + d : z);
                var scale = new Vector3(w / 2f, 1f, d / 2f);
                Spawn("SM_Build_Floor_2x2", floor, p, Quaternion.identity).transform.localScale = scale;
                Spawn("SM_Build_Ceiling_2x2", ceiling, p + Vector3.up * Ceiling, Quaternion.identity).transform.localScale = scale;
            }
        }

        // ------------------------------------------------------------------ graffiti, systemen

        /// <summary>
        /// Controls-graffiti op de blinde gevelmodule links van de ingang (baksteen x -8..-3,4 op het gelijkvloers),
        /// zo dicht mogelijk bij de ingang zodat hij binnen de scherpe focusafstand vanaf de startplek valt.
        /// </summary>
        static void PlaceGraffiti(Transform parent, StringBuilder log)
        {
            var go = Spawn("SM_Deco_Graffiti_Controls", parent, Vector3.zero, Quaternion.LookRotation(Vector3.back));
            var b = Bounds(go);
            const float maxWidth = 3.4f, rightEdge = -4.0f;
            if (b.size.x > maxWidth)
                go.transform.localScale *= maxWidth / b.size.x;
            b = Bounds(go);
            var outerFace = FrontZ - 0.15f - 0.01f;
            go.transform.position += new Vector3(rightEdge - b.max.x, 1.65f - b.center.y, outerFace - b.max.z);
            b = Bounds(go);
            log.AppendLine($"Graffiti op de voorgevel: {b.size.x:0.00} x {b.size.y:0.00} m rond ({b.center.x:0.0}, {b.center.y:0.0}, {b.max.z:0.00})");
        }

        static void AddInteriorZone(Transform systems, StringBuilder log)
        {
            var go = new GameObject("InteriorZone");
            go.transform.SetParent(systems, false);
            var min = new Vector3(-SideX, 0f, LobbyBackZ + 0.05f);
            var max = new Vector3(SideX, Ceiling + 0.2f, BackZ);
            go.transform.position = (min + max) / 2f;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = max - min;

            var focus = Object.FindObjectsByType<FocusOnTarget>(FindObjectsInactive.Include).FirstOrDefault();
            var zone = new SerializedObject(go.AddComponent<InteriorZone>());
            zone.FindProperty("outdoorFocus").objectReferenceValue = focus;
            zone.FindProperty("outdoorVolume").objectReferenceValue = focus != null ? focus.GetComponent<Volume>() : null;
            zone.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine($"InteriorZone {box.size.x:0.0} x {box.size.z:0.0} m (focus {(focus != null ? "gekoppeld" : "NIET gevonden")})");
        }

        static void AddToBuildSettings(StringBuilder log)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            log.AppendLine("Main staat op index 0 in de Build Settings");
        }

        // ------------------------------------------------------------------ hulpen

        static Transform Root(string name) => new GameObject(name).transform;

        static Transform Child(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing;
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static Vector3 Center(Transform t) => Bounds(t.gameObject).center;

        static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers.Skip(1))
                b.Encapsulate(r.bounds);
            return b;
        }
    }
}
