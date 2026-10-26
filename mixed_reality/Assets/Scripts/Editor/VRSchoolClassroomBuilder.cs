using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static VRSchool.Editor.VRSchoolBuildCore;

namespace VRSchool.Editor
{
    /// <summary>
    /// Fase 2 (klaslokaal): prefabs voor meubels/borden/IT/grab-props en de scene Classroom_01
    /// (10 x 8 m, 24 banken + leraarsplek). FBX'en komen uit blender/tools/build_classroom_kit.py.
    /// Vereist dat Fase 1 (gebouwkit) gebouwd is. Opnieuw uitvoeren is veilig.
    /// </summary>
    public static class VRSchoolClassroomBuilder
    {
        const string ScenePath = "Assets/Scenes/Classroom_01.unity";

        public static readonly AssetDef[] Assets =
        {
            new("SM_Class_Desk_A", "Classroom", Kind.Static),
            new("SM_Class_Chair_A", "Classroom", Kind.Static),
            new("SM_Class_TeacherDesk", "Classroom", Kind.Static),
            new("SM_Class_OfficeChair", "Classroom", Kind.Static),
            new("SM_Class_Chalkboard", "Classroom", Kind.StaticMesh),
            new("SM_Class_Whiteboard", "Classroom", Kind.StaticMesh),
            new("SM_Class_SmartBoard", "Classroom", Kind.StaticMesh),
            new("SM_Class_Projector", "Classroom", Kind.Static),
            new("SM_Class_ProjectorScreen", "Classroom", Kind.ProjectorScreen),
            new("SM_Class_Cabinet", "Classroom", Kind.Static),
            new("SM_Class_Bookshelf", "Classroom", Kind.StaticMesh),
            new("SM_Class_CoatRack", "Classroom", Kind.Static),
            new("SM_Class_TrashBin", "Classroom", Kind.Grab),
            new("SM_Class_WallClock", "Classroom", Kind.Clock),
            new("SM_Class_Laptop", "Classroom", Kind.LaptopOpen),
            new("SM_Class_Laptop_Closed", "Classroom", Kind.LaptopClosed, model: "SM_Class_Laptop"),
            new("SM_Class_PC_Monitor", "Classroom", Kind.Static),
            new("SM_Class_Keyboard", "Classroom", Kind.Static),
            new("SM_Class_Corkboard", "Classroom", Kind.Static),
            new("SM_Class_PaperNote", "Classroom", Kind.Pinned),
            new("SM_Class_Poster_Frame", "Classroom", Kind.Static),
            new("SM_Class_WorldMap", "Classroom", Kind.Static),

            new("SM_Prop_Book_A", "Props", Kind.Grab),
            new("SM_Prop_Book_B", "Props", Kind.Grab),
            new("SM_Prop_Book_C", "Props", Kind.Grab),
            new("SM_Prop_Notebook", "Props", Kind.Grab),
            new("SM_Prop_Pen", "Props", Kind.GrabRound),
            new("SM_Prop_Pencil", "Props", Kind.GrabRound),
            new("SM_Prop_Eraser", "Props", Kind.Grab),
            new("SM_Prop_Ruler", "Props", Kind.Grab),
            new("SM_Prop_Calculator", "Props", Kind.Grab),
            new("SM_Prop_PencilCase", "Props", Kind.GrabRound),
            new("SM_Prop_Backpack_A", "Props", Kind.Grab),
            new("SM_Prop_Backpack_B", "Props", Kind.Grab),
            new("SM_Prop_WaterBottle", "Props", Kind.GrabRound),
            new("SM_Prop_Chalk", "Props", Kind.GrabRound),
            new("SM_Prop_Marker", "Props", Kind.GrabRound),
        };

        [MenuItem("VR School/Build Phase 2 - Classroom")]
        public static void BuildAllMenu() => Debug.Log(BuildAll());

        public static string BuildAll()
        {
            var log = new StringBuilder();
            EnsureFolder("Assets/Scenes");
            AssetDatabase.Refresh();
            BuildMaterials(log);
            RemapModelMaterials(Assets, log);

            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                DetectAxes(log);
                BuildPrefabs(Assets, log);
                BuildScene(scene, log);
                EditorSceneManager.SaveScene(scene, ScenePath);
                log.AppendLine($"Scene opgeslagen: {ScenePath}");
            }
            finally
            {
                if (previousActive.IsValid())
                    SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ------------------------------------------------------------------ scene

        // Kamer 10 (x) x 8 (z) m. Bordwand = noord (z = 8), ramen = west (x = 0), deur = oost, kasten = zuid.
        const float W = 10f, D = 8f, Face = 0.1f;   // Face = halve wanddikte (muuroppervlak t.o.v. gridlijn)

        static void BuildScene(Scene scene, StringBuilder log)
        {
            var room = new GameObject("Classroom_01").transform;
            Transform Group(string name)
            {
                var t = new GameObject(name).transform;
                t.SetParent(room, false);
                return t;
            }
            var walls = Group("Walls");
            var floor = Group("Floor");
            var ceiling = Group("Ceiling");
            var front = Group("FrontWall");
            var teacher = Group("Teacher");
            var desks = Group("StudentDesks");
            var back = Group("BackWall");
            var side = Group("SideWall");
            var props = Group("Props");

            // --- Ruwbouw uit de Fase 1-kit
            VRSchoolKitBuilder.FloorAndCeiling(floor, ceiling, 5, 4);
            Run(walls, new Vector3(0, 0, D), Vector3.right, Vector3.back, "SM_Build_Wall_2m", "SM_Build_Wall_4m", "SM_Build_Wall_4m");
            Run(walls, new Vector3(0, 0, 0), Vector3.right, Vector3.forward, "SM_Build_Wall_4m", "SM_Build_Wall_4m", "SM_Build_Wall_2m");
            var west = Run(walls, new Vector3(0, 0, 0), Vector3.forward, Vector3.right,
                "SM_Build_Wall_Window", "SM_Build_Wall_Window", "SM_Build_Wall_Window", "SM_Build_Wall_Window");
            var east = Run(walls, new Vector3(W, 0, 0), Vector3.forward, Vector3.left, "SM_Build_Wall_4m", "SM_Build_Wall_2m", "SM_Build_Wall_Door");
            VRSchoolKitBuilder.Corners(walls, W, D);
            VRSchoolKitBuilder.WindowsWithRadiators(west, walls, props);

            var door = east.First(w => w.name == "SM_Build_Wall_Door");
            SpawnLocal("SM_Build_Door_Classroom", walls, door, 0.555f, 0f, 0f);
            SpawnLocal("SM_Build_LightSwitch", props, door, 1.75f, -0.1f, 1.1f);

            foreach (var x in new[] { 2f, 5f, 8f })
            foreach (var z in new[] { 1.5f, 4f, 6.5f })
                Spawn("SM_Build_Light_TL", ceiling, new Vector3(x, 3f, z), Quaternion.identity);
            Spawn("SM_Build_SmokeDetector", ceiling, new Vector3(5f, 3f, 3f), Quaternion.identity);
            Spawn("SM_Build_Speaker", props, new Vector3(9f, 2.5f, Face), Facing(Vector3.forward));
            Spawn("SM_Build_Socket", props, new Vector3(9.2f, 0.3f, D - Face), Facing(Vector3.back));
            Spawn("SM_Build_Socket", props, new Vector3(Face, 0.3f, 7.2f), Facing(Vector3.right));
            Spawn("SM_Build_FireExtinguisher", props, new Vector3(W - 0.25f, 0f, 5.6f), Quaternion.identity);

            // --- Bordwand (noord): smartboard, krijtbord, whiteboard met projectiescherm, klok
            var toRoom = Facing(Vector3.back);
            var frontZ = D - Face;
            Spawn("SM_Class_SmartBoard", front, new Vector3(1.7f, 0.9f, frontZ), toRoom);
            var chalkboard = Spawn("SM_Class_Chalkboard", front, new Vector3(5f, 0.9f, frontZ), toRoom);
            var whiteboard = Spawn("SM_Class_Whiteboard", front, new Vector3(8.2f, 0.9f, frontZ), toRoom);
            Spawn("SM_Class_ProjectorScreen", front, new Vector3(8.2f, 2.95f, frontZ), toRoom);
            Spawn("SM_Class_WallClock", front, new Vector3(5f, 2.55f, frontZ), toRoom);
            Spawn("SM_Class_Projector", ceiling, new Vector3(8.2f, 3f, 4.5f), Facing(Vector3.forward));
            // krijt en stiften in de goot (goot: 4 cm onder het bord, 6,5 cm voor de muur)
            OnTray("SM_Prop_Chalk", props, chalkboard.transform, -0.6f, 0.04f);
            OnTray("SM_Prop_Chalk", props, chalkboard.transform, 0.3f, 0.04f);
            OnTray("SM_Prop_Marker", props, whiteboard.transform, -0.4f, 0.02f);
            OnTray("SM_Prop_Marker", props, whiteboard.transform, 0.2f, 0.02f);

            // --- Leraarsplek: bureau met zitkant naar het bord, leerkracht kijkt naar de klas
            var deskTop = 0.75f;
            Spawn("SM_Class_TeacherDesk", teacher, new Vector3(7.6f, 0f, 6.6f), Facing(Vector3.forward));
            Spawn("SM_Class_OfficeChair", teacher, new Vector3(7.4f, 0f, 7.25f), Facing(Vector3.back));
            Spawn("SM_Class_PC_Monitor", teacher, new Vector3(7.95f, deskTop, 6.45f), Facing(Vector3.forward));
            Spawn("SM_Class_Keyboard", teacher, new Vector3(7.95f, deskTop, 6.78f), Facing(Vector3.forward));
            Spawn("SM_Class_Laptop", teacher, new Vector3(7.15f, deskTop, 6.65f), Facing(Vector3.forward) * Quaternion.Euler(0, 15, 0));
            Spawn("SM_Prop_Book_B", teacher, new Vector3(6.95f, deskTop, 6.4f), Quaternion.Euler(0, 80, 0));
            Spawn("SM_Prop_Notebook", teacher, new Vector3(7.5f, deskTop, 6.85f), Quaternion.Euler(0, -10, 0));

            // --- 24 leerlingenbanken: 3 duo-kolommen x 4 rijen. Bank: zitkant naar achter; stoel kijkt naar het bord.
            var rng = new System.Random(42);
            var columns = new[] { 1.95f, 2.65f, 4.65f, 5.35f, 7.35f, 8.05f };
            var rows = new[] { 5.2f, 4.0f, 2.8f, 1.6f };
            var studentTop = 0.76f;
            string[] deskProps = { "SM_Prop_Notebook", "SM_Prop_Book_A", "SM_Prop_Book_C", "SM_Prop_Pen", "SM_Prop_Pencil",
                                   "SM_Prop_Eraser", "SM_Prop_Ruler", "SM_Prop_Calculator", "SM_Prop_PencilCase", "SM_Prop_WaterBottle" };
            for (var c = 0; c < columns.Length; c++)
            {
                var outward = c % 2 == 0 ? -1f : 1f;   // kant weg van de duo-partner
                foreach (var z in rows)
                {
                    var x = columns[c];
                    Spawn("SM_Class_Desk_A", desks, new Vector3(x, 0f, z), Facing(Vector3.back));
                    Spawn("SM_Class_Chair_A", desks, new Vector3(x, 0f, z - 0.42f), Facing(Vector3.forward));
                    var count = rng.Next(1, 4);
                    for (var k = 0; k < count; k++)
                    {
                        var prop = deskProps[rng.Next(deskProps.Length)];
                        var pos = new Vector3(x + Range(rng, -0.2f, 0.2f), studentTop, z + Range(rng, -0.12f, 0.1f));
                        Spawn(prop, props, pos, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                    }
                    if (rng.NextDouble() < 0.5)
                    {
                        var bag = rng.NextDouble() < 0.5 ? "SM_Prop_Backpack_A" : "SM_Prop_Backpack_B";
                        Spawn(bag, props, new Vector3(x + outward * 0.5f, 0f, z - 0.35f), Quaternion.Euler(0f, Range(rng, -40f, 40f) + 90f * outward, 0f));
                    }
                }
            }

            // --- Achterwand (zuid): boekenrekken, kast, wereldkaart, posters, vuilbak
            var toFront = Facing(Vector3.forward);
            Spawn("SM_Class_Bookshelf", back, new Vector3(0.8f, 0f, Face + 0.17f), toFront);
            Spawn("SM_Class_Bookshelf", back, new Vector3(1.75f, 0f, Face + 0.17f), toFront);
            Spawn("SM_Class_Cabinet", back, new Vector3(3.1f, 0f, Face + 0.25f), toFront);
            Spawn("SM_Prop_Book_A", props, new Vector3(2.9f, 1.0f, 0.4f), Quaternion.Euler(0, 5, 0));
            Spawn("SM_Class_WorldMap", back, new Vector3(5.4f, 1.65f, Face), toFront);
            Spawn("SM_Class_Poster_Frame", back, new Vector3(7.3f, 1.55f, Face), toFront);
            Spawn("SM_Class_Poster_Frame", back, new Vector3(8.2f, 1.55f, Face), toFront);
            Spawn("SM_Class_TrashBin", props, new Vector3(9.5f, 0f, 0.45f), Quaternion.identity);

            // --- Zijwand (oost): prikbord met briefjes, kapstok
            var toWest = Facing(Vector3.left);
            var cork = Spawn("SM_Class_Corkboard", side, new Vector3(W - Face, 1.5f, 2.5f), toWest).transform;
            foreach (var (bx, bz) in new[] { (-0.35f, 0.15f), (-0.1f, -0.1f), (0.2f, 0.2f), (0.4f, -0.2f) })
                Spawn("SM_Class_PaperNote", props, cork.position + cork.rotation * B2U(bx, -0.015f, bz),
                    cork.rotation * Quaternion.AngleAxis(Range(rng, -8f, 8f), Vector3.forward));
            Spawn("SM_Class_CoatRack", side, new Vector3(W - Face, 1.65f, 4.7f), toWest);

            BuildShowcase(new Vector3(0f, 0f, -3f), Assets.Select(a => a.Prefab));
            PlaceXROrigin(scene, new Vector3(5f, 0f, 0.8f), Quaternion.identity, log);
            log.AppendLine($"Classroom_01: {desks.childCount / 2} banken, {props.childCount} props, " +
                           $"{room.GetComponentsInChildren<Transform>().Length} transforms");
        }

        static void OnTray(string prefab, Transform parent, Transform board, float x, float frame)
            => Spawn(prefab, parent, board.position + board.rotation * B2U(x, -0.065f, -frame), board.rotation);

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
