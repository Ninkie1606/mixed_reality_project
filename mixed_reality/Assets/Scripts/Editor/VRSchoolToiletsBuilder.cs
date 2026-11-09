using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static VRSchool.Editor.VRSchoolBuildCore;

namespace VRSchool.Editor
{
    /// <summary>
    /// Fase 4 (toiletten): prefabs voor cabine, toilet, lavabo, spiegel, handendroger en zeepdispenser,
    /// en de scene Toilets_01 (6 x 4 m). FBX'en komen uit blender/tools/build_toilets_kit.py.
    /// Vereist Fase 1 (gebouwkit) en Fase 2 (vuilbak). Opnieuw uitvoeren is veilig.
    /// </summary>
    public static class VRSchoolToiletsBuilder
    {
        const string ScenePath = "Assets/Scenes/Toilets_01.unity";

        public static readonly AssetDef[] Assets =
        {
            new("SM_WC_Stall", "Toilets", Kind.HingedCabinet),
            new("SM_WC_Toilet", "Toilets", Kind.Static),
            new("SM_WC_Sink", "Toilets", Kind.StaticMesh),
            new("SM_WC_Mirror", "Toilets", Kind.Static),
            new("SM_WC_HandDryer", "Toilets", Kind.Static),
            new("SM_WC_SoapDispenser", "Toilets", Kind.Static),
        };

        [MenuItem("VR School/Build Phase 4 - Toilets")]
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

        // Toiletruimte 6 (x) x 4 (z) m. Noord = 4 cabines, zuid = lavabo's met spiegels, oost = deur.
        const float W = 6f, D = 4f, Face = 0.1f;

        static void BuildScene(Scene scene, StringBuilder log)
        {
            var room = new GameObject("Toilets_01").transform;
            Transform Group(string name)
            {
                var t = new GameObject(name).transform;
                t.SetParent(room, false);
                return t;
            }
            var walls = Group("Walls");
            var floor = Group("Floor");
            var ceiling = Group("Ceiling");
            var stalls = Group("Stalls");
            var sinks = Group("Sinks");
            var props = Group("Props");

            // --- Ruwbouw uit de Fase 1-kit
            VRSchoolKitBuilder.FloorAndCeiling(floor, ceiling, 3, 2);
            Run(walls, new Vector3(0, 0, D), Vector3.right, Vector3.back, "SM_Build_Wall_4m", "SM_Build_Wall_2m");
            Run(walls, new Vector3(0, 0, 0), Vector3.right, Vector3.forward, "SM_Build_Wall_4m", "SM_Build_Wall_2m");
            Run(walls, new Vector3(0, 0, 0), Vector3.forward, Vector3.right, "SM_Build_Wall_4m");
            var east = Run(walls, new Vector3(W, 0, 0), Vector3.forward, Vector3.left, "SM_Build_Wall_Door", "SM_Build_Wall_2m");
            VRSchoolKitBuilder.Corners(walls, W, D);

            var door = east.First(w => w.name == "SM_Build_Wall_Door");
            SpawnLocal("SM_Build_Door_Classroom", walls, door, 0.555f, 0f, 0f);
            SpawnLocal("SM_Build_LightSwitch", props, door, 1.75f, -0.1f, 1.1f);

            foreach (var p in new[] { new Vector3(1.5f, 3f, 1.1f), new Vector3(1.5f, 3f, 3.2f), new Vector3(4.5f, 3f, 2f) })
                Spawn("SM_Build_Light_TL", ceiling, p, Quaternion.identity);
            Spawn("SM_Build_SmokeDetector", ceiling, new Vector3(3f, 3f, 1.1f), Quaternion.identity);

            // --- 4 cabines (0,9 m breed) tegen de noordwand, toilet in elke cabine
            var toSouth = Facing(Vector3.back);
            for (var i = 0; i < 4; i++)
            {
                var x = Face + 0.45f + i * 0.9f;
                Spawn("SM_WC_Stall", stalls, new Vector3(x, 0f, D - Face), toSouth);
                Spawn("SM_WC_Toilet", stalls, new Vector3(x, 0f, D - Face), toSouth);
            }

            // --- 3 lavabo's met spiegel en zeep (zuid), handendroger en vuilbak ernaast
            var toNorth = Facing(Vector3.forward);
            foreach (var x in new[] { 1.2f, 2.1f, 3.0f })
            {
                Spawn("SM_WC_Sink", sinks, new Vector3(x, 0f, Face), toNorth);
                Spawn("SM_WC_Mirror", sinks, new Vector3(x, 1.45f, Face), toNorth);
                Spawn("SM_WC_SoapDispenser", sinks, new Vector3(x + 0.45f, 1.1f, Face), toNorth);
            }
            Spawn("SM_WC_HandDryer", sinks, new Vector3(4.3f, 1.2f, Face), toNorth);
            Spawn("SM_Class_TrashBin", props, new Vector3(4.9f, 0f, 0.4f), Quaternion.identity);

            // Spiegels hebben een reflectieprobe nodig (anders weerspiegelen ze de skybox); baked, mee met de lightmap-bake.
            var probe = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(room, false);
            probe.transform.localPosition = new Vector3(W / 2, 1.5f, D / 2);
            probe.mode = ReflectionProbeMode.Baked;
            probe.size = new Vector3(W, 3f, D);
            probe.boxProjection = true;

            BuildShowcase(new Vector3(0f, 0f, -3f), Assets.Select(a => a.Prefab));
            PlaceXROrigin(scene, new Vector3(5f, 0f, 2.8f), Quaternion.Euler(0f, -90f, 0f), log);
            log.AppendLine($"Toilets_01: {stalls.childCount / 2} cabines, {sinks.childCount} lavabo-items, " +
                           $"{room.GetComponentsInChildren<Transform>().Length} transforms");
        }
    }
}
