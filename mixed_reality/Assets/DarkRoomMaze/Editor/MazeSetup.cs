using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkRoomMaze.EditorTools
{
    /// <summary>
    /// Tools > Dark Room Maze > Maak prefabs + scene.
    /// Maakt materialen en prefabs aan (bestaande worden NIET overschreven, dus je aanpassingen blijven)
    /// en bouwt een donkere scene met de XR Origin en een gegenereerd doolhof.
    /// </summary>
    static class MazeSetup
    {
        const string Root = "Assets/DarkRoomMaze";
        const string ScenePath = Root + "/Scenes/DarkRoomMaze.unity";
        const string XrOriginPath = "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab";

        [MenuItem("Tools/Dark Room Maze/Maak prefabs + scene")]
        static void CreateAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Dark Room Maze", $"{ScenePath} bestaat al. Overschrijven?", "Overschrijven", "Annuleren"))
                return;

            var prefabs = CreatePrefabs();
            BuildScene(prefabs);
        }

        [MenuItem("Tools/Dark Room Maze/Alleen prefabs aanmaken")]
        static void CreatePrefabsOnly()
        {
            CreatePrefabs();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(Root + "/Prefabs");
        }

        struct Prefabs
        {
            public GameObject wall, floor, ceiling, lamp, exit;
        }

        static Prefabs CreatePrefabs()
        {
            Folder(Root + "/Materials");
            Folder(Root + "/Prefabs");
            Folder(Root + "/Scenes");

            var wallMat = Mat("Maze_Wall", new Color(0.025f, 0.025f, 0.028f), 0.05f);
            var floorMat = Mat("Maze_Floor", new Color(0.012f, 0.012f, 0.014f), 0.2f);
            var ceilMat = Mat("Maze_Ceiling", new Color(0.01f, 0.01f, 0.01f), 0f);
            var lampMat = Mat("Maze_LampGlow", Color.black, 0.5f, new Color(1f, 0.55f, 0.3f) * 3f);
            var exitMat = Mat("Maze_ExitGlow", Color.black, 0.5f, new Color(1f, 0.85f, 0.6f) * 4f);

            var p = new Prefabs();

            // muur/vloer/plafond: 1x1x1 m, pivot onderaan in het midden; de generator schaalt de root
            p.wall = Prefab("Maze_Wall", root => Cube(root, "Mesh", new Vector3(0, 0.5f, 0), Vector3.one, wallMat));
            p.floor = Prefab("Maze_Floor", root => Cube(root, "Mesh", new Vector3(0, -0.5f, 0), Vector3.one, floorMat));
            p.ceiling = Prefab("Maze_Ceiling", root => Cube(root, "Mesh", new Vector3(0, 0.5f, 0), Vector3.one, ceilMat));

            // lamp: pivot = lichtpunt, klein gloeiend bolletje als armatuur
            p.lamp = Prefab("Maze_Lamp", root =>
            {
                var light = root.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.55f, 0.3f);
                light.intensity = 1.5f;
                light.range = 5f;
                light.shadows = LightShadows.None;
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "Bulb";
                bulb.transform.SetParent(root.transform, false);
                bulb.transform.localScale = Vector3.one * 0.12f;
                bulb.GetComponent<Renderer>().sharedMaterial = lampMat;
                Object.DestroyImmediate(bulb.GetComponent<Collider>());
            });

            // uitgang: gloeiende deur + warm licht dat het doolhof in schijnt (-Z)
            p.exit = Prefab("Maze_Exit", root =>
            {
                Cube(root, "Door", new Vector3(0, 1.2f, 0), new Vector3(1.6f, 2.4f, 0.1f), exitMat);
                var lightGo = new GameObject("Light");
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.localPosition = new Vector3(0, 1.3f, -0.8f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.85f, 0.6f);
                light.intensity = 3f;
                light.range = 6f;
                light.shadows = LightShadows.None;
            });

            AssetDatabase.SaveAssets();
            return p;
        }

        static void BuildScene(Prefabs p)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // donkere kamer: geen skybox, nauwelijks omgevingslicht, zwarte mist
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.01f, 0.01f, 0.012f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.black;
            RenderSettings.fogDensity = 0.12f;

            var gen = new GameObject("DarkRoomMaze").AddComponent<MazeGenerator>();
            gen.wallPrefab = p.wall;
            gen.floorPrefab = p.floor;
            gen.ceilingPrefab = p.ceiling;
            gen.lampPrefab = p.lamp;
            gen.exitPrefab = p.exit;

            var xrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPath);
            if (xrPrefab)
            {
                var xr = (GameObject)PrefabUtility.InstantiatePrefab(xrPrefab, scene);
                gen.player = xr.transform;
                foreach (var cam in xr.GetComponentsInChildren<Camera>(true))
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                }
            }
            else
            {
                Debug.LogWarning($"XR Origin niet gevonden op {XrOriginPath}; voeg zelf een speler toe en sleep die in 'Player'.");
            }

            gen.Generate();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = gen.gameObject;
        }

        // ---------- helpers ----------
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Material Mat(string name, Color baseColor, float smoothness, Color? emission = null)
        {
            var path = $"{Root}/Materials/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static GameObject Prefab(string name, System.Action<GameObject> build)
        {
            var path = $"{Root}/Prefabs/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) return existing;

            var go = new GameObject(name);
            build(go);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject Cube(GameObject parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.name = name;
            c.transform.SetParent(parent.transform, false);
            c.transform.localPosition = pos;
            c.transform.localScale = scale;
            c.GetComponent<Renderer>().sharedMaterial = mat;
            return c;
        }
    }
}
