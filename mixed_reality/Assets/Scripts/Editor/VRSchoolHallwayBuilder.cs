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
    /// Fase 3 (gang): prefabs voor lockers, bank, infoscherm, trofeeënkast, fotokader, plantenbak, fontein,
    /// drankautomaat en trofeeën, en de scene Hallway_01 (16 x 4 m). FBX'en komen uit blender/tools/build_hallway_kit.py.
    /// Vereist dat Fase 1 (gebouwkit) gebouwd is. Opnieuw uitvoeren is veilig.
    /// </summary>
    public static class VRSchoolHallwayBuilder
    {
        const string ScenePath = "Assets/Scenes/Hallway_01.unity";

        public static readonly AssetDef[] Assets =
        {
            new("SM_Hall_Locker_Single", "Hallway", Kind.HingedCabinet),
            new("SM_Hall_Locker_Row_5", "Hallway", Kind.HingedCabinet),
            new("SM_Hall_Bench", "Hallway", Kind.Static),
            new("SM_Hall_InfoScreen", "Hallway", Kind.Static),
            new("SM_Hall_TrophyCase", "Hallway", Kind.StaticMesh),
            new("SM_Hall_PictureFrame", "Hallway", Kind.Static),
            new("SM_Hall_Planter", "Hallway", Kind.Static),
            new("SM_Hall_Plant", "Hallway", Kind.Static),
            new("SM_Hall_WaterFountain", "Hallway", Kind.Static),
            new("SM_Hall_VendingMachine", "Hallway", Kind.Static),

            new("SM_Prop_Trophy_A", "Props", Kind.Grab),
            new("SM_Prop_Trophy_B", "Props", Kind.Grab),
        };

        [MenuItem("VR School/Build Phase 3 - Hallway")]
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

        // Gang 16 (x) x 4 (z) m. Noord = klasdeuren + lockers, zuid = ramen + vitrine/automaat,
        // west = dubbele deur, oost = nooddeur.
        const float L = 16f, D = 4f, Face = 0.1f;

        static void BuildScene(Scene scene, StringBuilder log)
        {
            var hall = new GameObject("Hallway_01").transform;
            Transform Group(string name)
            {
                var t = new GameObject(name).transform;
                t.SetParent(hall, false);
                return t;
            }
            var walls = Group("Walls");
            var floor = Group("Floor");
            var ceiling = Group("Ceiling");
            var lockers = Group("Lockers");
            var furniture = Group("Furniture");
            var props = Group("Props");

            // --- Ruwbouw uit de Fase 1-kit
            VRSchoolKitBuilder.FloorAndCeiling(floor, ceiling, 8, 2);
            var north = Run(walls, new Vector3(0, 0, D), Vector3.right, Vector3.back,
                "SM_Build_Wall_2m", "SM_Build_Wall_Door", "SM_Build_Wall_4m", "SM_Build_Wall_4m", "SM_Build_Wall_Door", "SM_Build_Wall_2m");
            var south = Run(walls, new Vector3(0, 0, 0), Vector3.right, Vector3.forward,
                "SM_Build_Wall_Window", "SM_Build_Wall_Window", "SM_Build_Wall_4m", "SM_Build_Wall_Window", "SM_Build_Wall_Window", "SM_Build_Wall_4m");
            var west = Run(walls, new Vector3(0, 0, 0), Vector3.forward, Vector3.right, "SM_Build_Wall_Door_Double");
            var east = Run(walls, new Vector3(L, 0, 0), Vector3.forward, Vector3.left, "SM_Build_Wall_2m", "SM_Build_Wall_Door");
            VRSchoolKitBuilder.Corners(walls, L, D);
            VRSchoolKitBuilder.WindowsWithRadiators(south, walls, props);

            foreach (var door in north.Where(w => w.name == "SM_Build_Wall_Door"))
            {
                SpawnLocal("SM_Build_Door_Classroom", walls, door, 0.555f, 0f, 0f);
                SpawnLocal("SM_Build_LightSwitch", props, door, 1.75f, -0.1f, 1.1f);
            }
            // Dubbele deur: root op het linkerscharnier = muurpivot + 1,15 m (zie Open punten Fase 1)
            var entrance = west[0];
            SpawnLocal("SM_Build_Door_Double", walls, entrance, 1.15f, 0f, 0f);
            SpawnLocal("SM_Build_ExitSign", props, entrance, 2.0f, -0.1f, 2.35f);
            var emergency = east.First(w => w.name == "SM_Build_Wall_Door");
            SpawnLocal("SM_Build_Door_Emergency", walls, emergency, 0.555f, 0f, 0f);
            SpawnLocal("SM_Build_ExitSign", props, emergency, 1.0f, -0.1f, 2.35f);

            foreach (var x in new[] { 2f, 6f, 10f, 14f })
                Spawn("SM_Build_Light_TL", ceiling, new Vector3(x, 3f, D / 2), Quaternion.identity);
            foreach (var x in new[] { 4f, 12f })
                Spawn("SM_Build_SmokeDetector", ceiling, new Vector3(x, 3f, D / 2), Quaternion.identity);
            Spawn("SM_Build_Speaker", props, new Vector3(7.85f, 2.5f, D - Face), Facing(Vector3.back));
            Spawn("SM_Build_FireExtinguisher", props, new Vector3(L - 0.25f, 0f, 1.4f), Quaternion.identity);
            Spawn("SM_Build_Socket", props, new Vector3(7.8f, 0.3f, Face), Facing(Vector3.forward));

            // --- Noordwand: twee lockerblokken (2 x 5 + 1) tussen de klasdeuren
            var toHall = Facing(Vector3.back);
            var lockerZ = D - Face;
            foreach (var x in new[] { 4.95f, 6.45f, 8.95f, 10.45f })
                Spawn("SM_Hall_Locker_Row_5", lockers, new Vector3(x, 0f, lockerZ), toHall);
            Spawn("SM_Hall_Locker_Single", lockers, new Vector3(7.35f, 0f, lockerZ), toHall);
            Spawn("SM_Hall_Locker_Single", lockers, new Vector3(11.35f, 0f, lockerZ), toHall);
            Spawn("SM_Hall_PictureFrame", furniture, new Vector3(1.3f, 1.6f, lockerZ), toHall);
            Spawn("SM_Hall_PictureFrame", furniture, new Vector3(15f, 1.6f, lockerZ), toHall);

            // --- Zuidwand: banken voor de ramen, vitrine + infoscherm, drankautomaat + fontein
            var toNorth = Facing(Vector3.forward);
            foreach (var x in new[] { 2f, 10f })
                Spawn("SM_Hall_Bench", furniture, new Vector3(x, 0f, 0.6f), toNorth);
            var trophyCase = Spawn("SM_Hall_TrophyCase", furniture, new Vector3(5.2f, 0f, Face), toNorth).transform;
            Spawn("SM_Hall_InfoScreen", furniture, new Vector3(7f, 1.7f, Face), toNorth);
            Spawn("SM_Hall_VendingMachine", furniture, new Vector3(13f, 0f, Face), toNorth);
            Spawn("SM_Hall_WaterFountain", furniture, new Vector3(14.3f, 0f, Face), toNorth);

            // trofeeën op de onderkast en de twee glasplaten (bovenkant op 0,80 / 1,155 / 1,505 m)
            var rng = new System.Random(7);
            var count = 0;
            foreach (var bz in new[] { 0.8f, 1.156f, 1.506f })
            foreach (var bx in new[] { -0.35f, 0f, 0.35f })
            {
                var prefab = count++ % 2 == 0 ? "SM_Prop_Trophy_A" : "SM_Prop_Trophy_B";
                Spawn(prefab, props, trophyCase.position + trophyCase.rotation * B2U(bx, -0.22f, bz),
                    trophyCase.rotation * Quaternion.Euler(0f, (float)rng.NextDouble() * 30f - 15f, 0f));
            }

            // --- Planten in de hoeken (plant op de aarde van de bak)
            foreach (var p in new[] { new Vector3(0.45f, 0f, D - 0.55f), new Vector3(0.45f, 0f, 0.55f), new Vector3(L - 0.6f, 0f, 0.45f) })
            {
                Spawn("SM_Hall_Planter", furniture, p, Quaternion.identity);
                Spawn("SM_Hall_Plant", furniture, p + Vector3.up * 0.40f, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
            }

            BuildShowcase(new Vector3(0f, 0f, -3f), Assets.Select(a => a.Prefab));
            PlaceXROrigin(scene, new Vector3(1.6f, 0f, D / 2), Quaternion.Euler(0f, 90f, 0f), log);
            log.AppendLine($"Hallway_01: {lockers.childCount} lockerblokken, {props.childCount} props, " +
                           $"{hall.GetComponentsInChildren<Transform>().Length} transforms");
        }
    }
}
