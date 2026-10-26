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
    /// Fase 1 (modulaire gebouwkit): URP-materialen, prefabs en de testscene Test_Modular.
    /// FBX'en komen uit blender/tools/build_modular_kit.py. Opnieuw uitvoeren is veilig (overschrijft, GUID's blijven).
    /// </summary>
    public static class VRSchoolKitBuilder
    {
        const string TestScenePath = "Assets/Scenes/Test_Modular.unity";

        public static readonly AssetDef[] Assets =
        {
            new("SM_Build_Wall_2m", "Building", Kind.Static),
            new("SM_Build_Wall_4m", "Building", Kind.Static),
            new("SM_Build_Wall_Corner_In", "Building", Kind.Static),
            new("SM_Build_Wall_Corner_Out", "Building", Kind.Static),
            new("SM_Build_Wall_Window", "Building", Kind.StaticUcx),
            new("SM_Build_Wall_Door", "Building", Kind.StaticUcx),
            new("SM_Build_Wall_Door_Double", "Building", Kind.StaticUcx),
            new("SM_Build_Floor_2x2", "Building", Kind.Floor),
            new("SM_Build_Ceiling_2x2", "Building", Kind.Static),
            new("SM_Build_Stairs_Straight", "Building", Kind.StaticMeshCollider),
            new("SM_Build_Railing", "Building", Kind.Static),
            new("SM_Build_DoorHandle", "Building", Kind.Handle),          // vóór de deuren: wordt erin genest
            new("SM_Build_Door_Classroom", "Building", Kind.Door),
            new("SM_Build_Door_Emergency", "Building", Kind.Door),
            new("SM_Build_Door_Double", "Building", Kind.DoubleDoor),
            new("SM_Build_Window_Glass", "Building", Kind.Static),
            new("SM_Build_Light_TL", "Building", Kind.Light),
            new("SM_Build_Radiator", "Building", Kind.Static),
            new("SM_Build_Socket", "Building", Kind.Static),
            new("SM_Build_LightSwitch", "Building", Kind.Static),
            new("SM_Build_FireExtinguisher", "Building", Kind.Grab),
            new("SM_Build_ExitSign", "Building", Kind.Static),
            new("SM_Build_SmokeDetector", "Building", Kind.Static),
            new("SM_Build_Speaker", "Building", Kind.Static),
        };

        [MenuItem("VR School/Build Phase 1 - Modular Kit")]
        public static void BuildAllMenu() => Debug.Log(BuildAll());

        public static string BuildAll()
        {
            var log = new StringBuilder();
            EnsureFolder("Assets/Scenes");
            AssetDatabase.Refresh();
            BuildMaterials(log);
            RemapModelMaterials(Assets, log);

            // Alles gebeurt in een aparte (additieve) scene, zodat de open scene van de gebruiker onaangeroerd blijft.
            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                DetectAxes(log);
                BuildPrefabs(Assets, log);
                BuildTestScene(scene, log);
                EditorSceneManager.SaveScene(scene, TestScenePath);
                log.AppendLine($"Scene opgeslagen: {TestScenePath}");
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

        static void BuildTestScene(Scene scene, StringBuilder log)
        {
            var room = new GameObject("Room_8x6").transform;
            var walls = new GameObject("Walls").transform;
            var props = new GameObject("Props").transform;
            var floor = new GameObject("Floor").transform;
            var ceiling = new GameObject("Ceiling").transform;
            foreach (var t in new[] { walls, props, floor, ceiling })
                t.SetParent(room, false);

            FloorAndCeiling(floor, ceiling, 4, 3);

            // Muren (tegen de klok in, kamerzijde naar binnen)
            var south = Run(walls, new Vector3(0, 0, 0), Vector3.right, Vector3.forward, "SM_Build_Wall_2m", "SM_Build_Wall_Door", "SM_Build_Wall_4m");
            var north = Run(walls, new Vector3(0, 0, 6), Vector3.right, Vector3.back, "SM_Build_Wall_Window", "SM_Build_Wall_Window", "SM_Build_Wall_Window", "SM_Build_Wall_2m");
            var west = Run(walls, new Vector3(0, 0, 0), Vector3.forward, Vector3.right, "SM_Build_Wall_4m", "SM_Build_Wall_2m");
            var east = Run(walls, new Vector3(8, 0, 0), Vector3.forward, Vector3.left, "SM_Build_Wall_Door", "SM_Build_Wall_4m");
            Corners(walls, 8, 6);

            // Ramen: glas + radiator onder elk raam
            WindowsWithRadiators(north, walls, props);

            // Klasdeur (zuid) + nooddeur (oost), scharnier tegen het kozijn
            var classDoor = south.First(w => w.name == "SM_Build_Wall_Door");
            SpawnLocal("SM_Build_Door_Classroom", walls, classDoor, 0.555f, 0f, 0f);
            SpawnLocal("SM_Build_LightSwitch", props, classDoor, 1.75f, -0.1f, 1.1f);
            SpawnLocal("SM_Build_ExitSign", props, classDoor, 1.0f, -0.1f, 2.35f);
            var emergency = east.First(w => w.name == "SM_Build_Wall_Door");
            SpawnLocal("SM_Build_Door_Emergency", walls, emergency, 0.555f, 0f, 0f);
            SpawnLocal("SM_Build_ExitSign", props, emergency, 1.0f, -0.1f, 2.35f);

            var south4 = south.First(w => w.name == "SM_Build_Wall_4m");
            SpawnLocal("SM_Build_Socket", props, south4, 1.0f, -0.1f, 0.3f);
            SpawnLocal("SM_Build_Socket", props, south4, 3.0f, -0.1f, 0.3f);
            SpawnLocal("SM_Build_Speaker", props, west.First(w => w.name == "SM_Build_Wall_4m"), 2.0f, -0.1f, 2.5f);
            SpawnLocal("SM_Build_FireExtinguisher", props, east.First(w => w.name == "SM_Build_Wall_4m"), 0.4f, -0.25f, 0f);

            foreach (var x in new[] { 2f, 6f })
            foreach (var z in new[] { 1.5f, 4.5f })
                Spawn("SM_Build_Light_TL", ceiling, new Vector3(x, 3f, z), Quaternion.identity);
            Spawn("SM_Build_SmokeDetector", ceiling, new Vector3(4f, 3f, 3f), Quaternion.identity);

            BuildShowcase(new Vector3(0f, 0f, -4f), Assets.Select(a => a.Prefab));
            PlaceXROrigin(scene, new Vector3(4f, 0f, 1.5f), Quaternion.identity, log);
            log.AppendLine($"Testscene: kamer 8 x 6 m, {room.GetComponentsInChildren<Transform>().Length} transforms");
        }

        // ------------------------------------------------------------------ gedeelde kamerhulpen

        /// <summary>Vloer- en plafondtegels van 2 x 2 m, vanaf de oorsprong, tilesX x tilesZ.</summary>
        public static void FloorAndCeiling(Transform floor, Transform ceiling, int tilesX, int tilesZ)
        {
            // Pivot zit in een hoek en de tegel loopt langs Blender +X/+Y; verschuif zodat hij [i*2, i*2+2] x [j*2, j*2+2] bedekt.
            var corner = B2U(1f, 1f, 0f);
            for (var i = 0; i < tilesX; i++)
            for (var j = 0; j < tilesZ; j++)
            {
                var p = new Vector3(i * 2f + (corner.x < 0 ? 2f : 0f), 0f, j * 2f + (corner.z < 0 ? 2f : 0f));
                Spawn("SM_Build_Floor_2x2", floor, p, Quaternion.identity);
                Spawn("SM_Build_Ceiling_2x2", ceiling, p + Vector3.up * 3f, Quaternion.identity);
            }
        }

        public static void Corners(Transform walls, float width, float depth)
        {
            foreach (var c in new[] { new Vector3(0, 0, 0), new Vector3(width, 0, 0), new Vector3(width, 0, depth), new Vector3(0, 0, depth) })
                Spawn("SM_Build_Wall_Corner_In", walls, c, Quaternion.identity);
        }

        public static void WindowsWithRadiators(System.Collections.Generic.IEnumerable<(string name, Vector3 pivot, Quaternion rot)> run,
            Transform walls, Transform props)
        {
            foreach (var w in run.Where(w => w.name == "SM_Build_Wall_Window"))
            {
                SpawnLocal("SM_Build_Window_Glass", walls, w, 0.35f, 0f, 0.95f);
                SpawnLocal("SM_Build_Radiator", props, w, 1.0f, -0.1f, 0f);
            }
        }
    }
}
