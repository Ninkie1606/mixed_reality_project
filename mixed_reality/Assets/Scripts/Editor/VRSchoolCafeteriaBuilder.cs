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
    /// Fase 4 (refter): prefabs voor tafel, stoel, buffet-toog, microgolfoven, dienblad en brooddoos,
    /// en de scene Cafeteria_01 (12 x 10 m). FBX'en komen uit blender/tools/build_cafeteria_kit.py.
    /// Vereist Fase 1 (gebouwkit) en Fase 2 (kast, vuilbak, drinkfles, rugzak). Opnieuw uitvoeren is veilig.
    /// </summary>
    public static class VRSchoolCafeteriaBuilder
    {
        const string ScenePath = "Assets/Scenes/Cafeteria_01.unity";

        public static readonly AssetDef[] Assets =
        {
            new("SM_Cafe_LongTable", "Cafeteria", Kind.Static),
            new("SM_Cafe_Chair", "Cafeteria", Kind.Static),
            new("SM_Cafe_Counter", "Cafeteria", Kind.StaticMesh),
            new("SM_Cafe_Microwave", "Cafeteria", Kind.HingedCabinet),

            new("SM_Prop_Tray", "Props", Kind.Grab),
            new("SM_Prop_LunchBox", "Props", Kind.Grab),
        };

        [MenuItem("VR School/Build Phase 4 - Cafeteria")]
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

        // Refter 12 (x) x 10 (z) m. Noord = buffet, zuid = ramen, west = microgolfovens, oost = dubbele deur (ingang).
        const float W = 12f, D = 10f, Face = 0.1f;
        const float TableTop = 0.75f, TrayRim = 0.021f;

        static void BuildScene(Scene scene, StringBuilder log)
        {
            var room = new GameObject("Cafeteria_01").transform;
            Transform Group(string name)
            {
                var t = new GameObject(name).transform;
                t.SetParent(room, false);
                return t;
            }
            var walls = Group("Walls");
            var floor = Group("Floor");
            var ceiling = Group("Ceiling");
            var serving = Group("Serving");
            var tables = Group("Tables");
            var props = Group("Props");

            // --- Ruwbouw uit de Fase 1-kit
            VRSchoolKitBuilder.FloorAndCeiling(floor, ceiling, 6, 5);
            Run(walls, new Vector3(0, 0, D), Vector3.right, Vector3.back, "SM_Build_Wall_4m", "SM_Build_Wall_4m", "SM_Build_Wall_4m");
            var south = Run(walls, new Vector3(0, 0, 0), Vector3.right, Vector3.forward,
                "SM_Build_Wall_Window", "SM_Build_Wall_Window", "SM_Build_Wall_Window",
                "SM_Build_Wall_Window", "SM_Build_Wall_Window", "SM_Build_Wall_Window");
            Run(walls, new Vector3(0, 0, 0), Vector3.forward, Vector3.right, "SM_Build_Wall_4m", "SM_Build_Wall_4m", "SM_Build_Wall_2m");
            var east = Run(walls, new Vector3(W, 0, 0), Vector3.forward, Vector3.left, "SM_Build_Wall_2m", "SM_Build_Wall_Door_Double", "SM_Build_Wall_4m");
            VRSchoolKitBuilder.Corners(walls, W, D);
            VRSchoolKitBuilder.WindowsWithRadiators(south, walls, props);

            var entrance = east.First(w => w.name == "SM_Build_Wall_Door_Double");
            SpawnLocal("SM_Build_Door_Double", walls, entrance, 1.15f, 0f, 0f);
            SpawnLocal("SM_Build_ExitSign", props, entrance, 2.0f, -0.1f, 2.35f);
            SpawnLocal("SM_Build_LightSwitch", props, entrance, 3.25f, -0.1f, 1.1f);

            foreach (var x in new[] { 2f, 6f, 10f })
            foreach (var z in new[] { 2f, 5f, 8f })
                Spawn("SM_Build_Light_TL", ceiling, new Vector3(x, 3f, z), Quaternion.identity);
            foreach (var x in new[] { 4f, 8f })
                Spawn("SM_Build_SmokeDetector", ceiling, new Vector3(x, 3f, 5f), Quaternion.identity);
            Spawn("SM_Build_Speaker", props, new Vector3(11f, 2.6f, D - Face), Facing(Vector3.back));
            Spawn("SM_Build_FireExtinguisher", props, new Vector3(W - 0.25f, 0f, 7f), Quaternion.identity);
            Spawn("SM_Class_WallClock", props, new Vector3(Face, 2.4f, 3f), Facing(Vector3.right));
            Spawn("SM_Class_Poster_Frame", props, new Vector3(10.2f, 1.6f, D - Face), Facing(Vector3.back));
            foreach (var p in new[] { new Vector3(11.5f, 0f, 1.0f), new Vector3(11.5f, 0f, 6.6f), new Vector3(0.5f, 0f, 9.4f) })
                Spawn("SM_Class_TrashBin", props, p, Quaternion.identity);

            // --- Buffet (noord): kast met dienbladen aan het begin van de rij, twee togen, klantenzijde naar het zuiden
            var toSouth = Facing(Vector3.back);
            Spawn("SM_Class_Cabinet", serving, new Vector3(2.25f, 0f, 8.6f), toSouth);
            for (var k = 0; k < 6; k++)
                Spawn("SM_Prop_Tray", props, new Vector3(2.25f, 1.0f + k * TrayRim, 8.6f), toSouth);
            foreach (var x in new[] { 4.3f, 7.4f })
                Spawn("SM_Cafe_Counter", serving, new Vector3(x, 0f, 8.6f), toSouth);

            // --- Microgolfovens (west) op twee kasten
            var toEast = Facing(Vector3.right);
            foreach (var z in new[] { 6.6f, 7.7f })
            {
                Spawn("SM_Class_Cabinet", serving, new Vector3(Face + 0.25f, 0f, z), toEast);
                Spawn("SM_Cafe_Microwave", serving, new Vector3(Face + 0.25f, 1.0f, z), toEast);
            }

            // --- 9 tafels (3 x 3) met elk 8 stoelen; dienbladen, brooddozen en flessen reproduceerbaar verspreid (seed 11)
            var rng = new System.Random(11);
            var seats = new[] { -0.9f, -0.3f, 0.3f, 0.9f };
            foreach (var x in new[] { 2.5f, 6.0f, 9.5f })
            foreach (var z in new[] { 1.6f, 4.0f, 6.4f })
            {
                Spawn("SM_Cafe_LongTable", tables, new Vector3(x, 0f, z), Quaternion.identity);
                foreach (var side in new[] { -1f, 1f })
                {
                    var towardTable = Facing(side < 0 ? Vector3.forward : Vector3.back);
                    foreach (var dx in seats)
                    {
                        Spawn("SM_Cafe_Chair", tables, new Vector3(x + dx, 0f, z + side * 0.6f), towardTable);
                        var spot = new Vector3(x + dx, TableTop, z + side * 0.18f);
                        var roll = rng.NextDouble();
                        if (roll < 0.4)
                        {
                            Spawn("SM_Prop_Tray", props, spot, towardTable * Quaternion.Euler(0f, Range(rng, -6f, 6f), 0f));
                            if (rng.NextDouble() < 0.6)
                                Spawn("SM_Prop_LunchBox", props, spot + new Vector3(Range(rng, -0.08f, 0.08f), TrayRim, 0f),
                                    Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                        }
                        else if (roll < 0.55)
                        {
                            Spawn("SM_Prop_LunchBox", props, spot, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                        }
                        if (rng.NextDouble() < 0.2)
                            Spawn("SM_Prop_WaterBottle", props, spot + new Vector3(0.29f, 0f, side * 0.1f), Quaternion.identity);
                        if (rng.NextDouble() < 0.12)
                            Spawn(rng.NextDouble() < 0.5 ? "SM_Prop_Backpack_A" : "SM_Prop_Backpack_B", props,
                                new Vector3(x + dx + 0.28f, 0f, z + side * 0.75f), towardTable * Quaternion.Euler(0f, 90f, 0f));
                    }
                }
            }

            BuildShowcase(new Vector3(0f, 0f, -3f), Assets.Select(a => a.Prefab));
            PlaceXROrigin(scene, new Vector3(11f, 0f, 4f), Quaternion.Euler(0f, -90f, 0f), log);
            log.AppendLine($"Cafeteria_01: {tables.childCount} tafels+stoelen, {props.childCount} props, " +
                           $"{room.GetComponentsInChildren<Transform>().Length} transforms");
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
