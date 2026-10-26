using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using VRSchool.Interaction;
using VRSchool.Visual;
using static VRSchool.Editor.VRSchoolBuildCore;

namespace VRSchool.Editor
{
    /// <summary>
    /// Fase 5 (buiten): schoolingang. Prefabs voor gevel, ingang, grond en buitenmeubilair, en de scene Entrance_01:
    /// de ingang is helder (warm licht, scherp), de omgeving vervaagt (Depth of Field + mist, gestuurd door FocusOnTarget),
    /// zodat de blik van de gebruiker naar de school geleid wordt. FBX'en komen uit blender/tools/build_outdoor_kit.py.
    /// Opnieuw uitvoeren is veilig.
    /// </summary>
    public static class VRSchoolEntranceBuilder
    {
        const string ScenePath = "Assets/Scenes/Entrance_01.unity";
        const string ProfilePath = "Assets/Settings/Volumes/VP_Entrance_Focus.asset";
        const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        public static readonly AssetDef[] Assets =
        {
            new("SM_Out_Facade_4m", "Outdoor", Kind.Static),
            new("SM_Out_Facade_Corner", "Outdoor", Kind.Static),
            new("SM_Out_Facade_Entrance_8m", "Outdoor", Kind.HingedCabinet),
            new("SM_Out_Ground_Paving_4x4", "Outdoor", Kind.Floor),
            new("SM_Out_Ground_Grass_20x20", "Outdoor", Kind.Floor),
            new("SM_Out_SchoolSign", "Outdoor", Kind.Static),
            new("SM_Out_Bench", "Outdoor", Kind.Static),
            new("SM_Out_BikeRack", "Outdoor", Kind.Static),
            new("SM_Out_Tree_A", "Outdoor", Kind.Static),
            new("SM_Out_Tree_B", "Outdoor", Kind.Static),
            new("SM_Out_Fence_Segment", "Outdoor", Kind.Static),
            new("SM_Out_Gate", "Outdoor", Kind.HingedCabinet),
            new("SM_Out_Lamppost", "Outdoor", Kind.StreetLight),
        };

        [MenuItem("VR School/Build Phase 5 - Entrance")]
        public static void BuildAllMenu() => Debug.Log(BuildAll());

        public static string BuildAll()
        {
            var log = new StringBuilder();
            EnsureFolder("Assets/Scenes");
            AssetDatabase.Refresh();
            BuildMaterials(log);
            RemapModelMaterials(Assets, log);
            EnsurePostProcessing(log);
            var profile = BuildFocusProfile(log);

            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                DetectAxes(log);
                BuildPrefabs(Assets, log);
                BuildScene(scene, profile, log);
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

        // ------------------------------------------------------------------ post-processing

        /// <summary>De URP-renderers van het project hadden geen PostProcessData (= post-processing uit). Alleen camera's
        /// met renderPostProcessing aan betalen ervoor; in dit project is dat enkel de camera van Entrance_01.</summary>
        static void EnsurePostProcessing(StringBuilder log)
        {
            var data = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
            foreach (var path in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Settings" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (renderer == null || renderer.postProcessData != null)
                    continue;
                renderer.postProcessData = data;
                renderer.SetDirty();               // herbouwt de actieve renderer, anders blijft post-processing uit tot een herstart
                EditorUtility.SetDirty(renderer);
                log.AppendLine($"Post-processing aangezet op renderer {path}");
            }
        }

        static VolumeProfile BuildFocusProfile(StringBuilder log)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(ProfilePath).Replace('\\', '/'));
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            // Startwaarden voor de editor; in Play mode verschuift FocusOnTarget de blur mee met de afstand tot de ingang.
            var dof = Override<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(15f);
            dof.gaussianEnd.Override(20f);
            dof.gaussianMaxRadius.Override(1.5f);
            dof.highQualitySampling.Override(false);
            var vignette = Override<Vignette>(profile);
            vignette.intensity.Override(0.4f);
            vignette.smoothness.Override(0.5f);
            EditorUtility.SetDirty(profile);
            log.AppendLine($"Volumeprofiel: {ProfilePath} (Depth of Field Gaussian + Vignette)");
            return profile;
        }

        static T Override<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
                return component;
            component = profile.Add<T>();
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);   // sub-asset, anders gaat het bij opslaan verloren
            return component;
        }

        // ------------------------------------------------------------------ scene

        // Gevel op z = 16 (buitenkant naar het zuiden), plein z 8..16, pad z 0..8, hek met poort op z = 0.
        // Gebruiker start net binnen de poort en kijkt naar het noorden, recht op de ingang.
        const float FacadeZ = 16f;

        static void BuildScene(Scene scene, VolumeProfile profile, StringBuilder log)
        {
            var site = new GameObject("Entrance_01").transform;
            Transform Group(string name)
            {
                var t = new GameObject(name).transform;
                t.SetParent(site, false);
                return t;
            }
            var building = Group("Building");
            var ground = Group("Ground");
            var street = Group("Street");
            var green = Group("Greenery");
            var lighting = Group("Lighting");

            // --- Grond: 4 grasvelden van 20 x 20 m, plein (4 x 2 tegels) en pad (1 x 2 tegels) van 4 x 4 m
            foreach (var x0 in new[] { -20f, 0f })
            foreach (var z0 in new[] { -12f, 8f })
                Tile("SM_Out_Ground_Grass_20x20", ground, x0, z0, 20f);
            foreach (var x0 in new[] { -8f, -4f, 0f, 4f })
            foreach (var z0 in new[] { 8f, 12f })
                Tile("SM_Out_Ground_Paving_4x4", ground, x0, z0, 4f);
            foreach (var z0 in new[] { 0f, 4f })
                Tile("SM_Out_Ground_Paving_4x4", ground, -2f, z0, 4f);

            // --- Gebouw: gevel van 32 m met de ingang in het midden + zijgevels van 8 m
            var outward = Vector3.back;
            var front = Run(building, new Vector3(-16f, 0f, FacadeZ), Vector3.right, outward,
                "SM_Out_Facade_4m", "SM_Out_Facade_4m", "SM_Out_Facade_4m", "SM_Out_Facade_Entrance_8m",
                "SM_Out_Facade_4m", "SM_Out_Facade_4m", "SM_Out_Facade_4m");
            Run(building, new Vector3(-16f, 0f, FacadeZ), Vector3.forward, Vector3.left, "SM_Out_Facade_4m", "SM_Out_Facade_4m");
            Run(building, new Vector3(16f, 0f, FacadeZ), Vector3.forward, Vector3.right, "SM_Out_Facade_4m", "SM_Out_Facade_4m");
            foreach (var x in new[] { -16f, 16f })
                Spawn("SM_Out_Facade_Corner", building, new Vector3(x, 0f, FacadeZ), Quaternion.identity);

            var entrance = front.First(w => w.name == "SM_Out_Facade_Entrance_8m");
            Vector3 AtEntrance(float bx, float by, float bz) => entrance.pivot + entrance.rot * B2U(bx, by, bz);
            var focus = new GameObject("FocusTarget").transform;
            focus.SetParent(building, false);
            focus.position = AtEntrance(4f, 0f, 1.5f);

            // --- Voorplein: naambord, banken, fietsrekken, lantaarnpalen langs het pad
            Spawn("SM_Out_SchoolSign", street, new Vector3(4.2f, 0f, 11f), Facing(new Vector3(-0.35f, 0f, -1f)));
            Spawn("SM_Out_Bench", street, new Vector3(-6f, 0f, 11f), Facing(Vector3.right));
            Spawn("SM_Out_Bench", street, new Vector3(6.5f, 0f, 13f), Facing(Vector3.left));
            foreach (var x in new[] { -10.5f, -13.5f })
                Spawn("SM_Out_BikeRack", street, new Vector3(x, 0f, FacadeZ - 1.2f), Facing(outward));
            foreach (var x in new[] { -2.7f, 2.7f })
            foreach (var z in new[] { 1.5f, 6f })
                Spawn("SM_Out_Lamppost", street, new Vector3(x, 0f, z), Quaternion.identity);
            foreach (var x in new[] { -7.5f, 7.5f })
                Spawn("SM_Out_Lamppost", street, new Vector3(x, 0f, 9f), Quaternion.identity);

            // --- Hek met open poort op z = 0
            Run(street, new Vector3(-18f, 0f, 0f), Vector3.right, outward, Enumerable.Repeat("SM_Out_Fence_Segment", 8).ToArray());
            Run(street, new Vector3(2f, 0f, 0f), Vector3.right, outward, Enumerable.Repeat("SM_Out_Fence_Segment", 8).ToArray());
            var gate = Spawn("SM_Out_Gate", street, new Vector3(-2f, 0f, 0f), Facing(outward));
            foreach (var leaf in gate.GetComponentsInChildren<SwingDoor>())
            {
                var so = new SerializedObject(leaf);
                so.FindProperty("startOpen").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // --- Groen rond het plein en verder weg (wordt vaag)
            var rng = new System.Random(5);
            foreach (var (x, z) in new[] { (-11f, 6f), (-14f, 12.5f), (11f, 5.5f), (14f, 12f), (-6f, -5f), (6f, -6f), (-15f, -4f),
                                           (16f, -2f), (-19f, 6f), (19f, 8f), (-9f, -10f), (10f, -10f), (-18f, 13f), (18f, 13.5f) })
                Spawn(rng.NextDouble() < 0.6 ? "SM_Out_Tree_A" : "SM_Out_Tree_B", green, new Vector3(x, 0f, z),
                    Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));

            // --- Licht: zon in de rug van de gebruiker op de gevel, warme spots onder de luifel, inkomhal verlicht
            var sun = scene.GetRootGameObjects().Select(g => g.GetComponent<Light>()).FirstOrDefault(l => l != null && l.type == LightType.Directional);
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(32f, 20f, 0f);
                sun.intensity = 0.7f;
                sun.color = new Color(1f, 0.94f, 0.84f);
                sun.transform.SetParent(lighting, true);
            }
            foreach (var bx in new[] { 3.3f, 4.7f })
                AddLight(lighting, "CanopySpot", LightType.Spot, AtEntrance(bx, -1.2f, 2.95f), Quaternion.Euler(90f, 0f, 0f), 6f, 4f);
            AddLight(lighting, "LobbyLight", LightType.Point, AtEntrance(4f, 1.65f, 2.6f), Quaternion.identity, 5f, 2f);

            // --- Focus: lineaire mist + globaal volume (Depth of Field + vignet), gestuurd door de afstand tot de ingang
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.70f, 0.74f, 0.78f);
            RenderSettings.fogStartDistance = 15f;
            RenderSettings.fogEndDistance = 40f;
            var volumeGo = new GameObject("FocusVolume");
            volumeGo.transform.SetParent(lighting, false);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            var focusOnTarget = new SerializedObject(volumeGo.AddComponent<FocusOnTarget>());
            focusOnTarget.FindProperty("target").objectReferenceValue = focus;
            focusOnTarget.FindProperty("volume").objectReferenceValue = volume;
            focusOnTarget.ApplyModifiedPropertiesWithoutUndo();

            BuildShowcase(new Vector3(-16f, 0f, -16f), Assets.Where(a => !a.Prefab.Contains("Ground")).Select(a => a.Prefab));
            PlaceXROrigin(scene, new Vector3(0f, 0f, 1.5f), Quaternion.identity, log);
            var cameras = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).ToArray();
            foreach (var cam in cameras)
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            log.AppendLine($"Entrance_01: gevel 32 m, focus op de ingang ({Vector3.Distance(new Vector3(0f, 1.6f, 1.5f), focus.position):F1} m van de start), " +
                           $"post-processing aan op {cameras.Length} camera('s), {site.GetComponentsInChildren<Transform>().Length} transforms");
        }

        /// <summary>Grond-/vloertegel met de pivot in een hoek: zo plaatsen dat hij [x0, x0+size] x [z0, z0+size] bedekt.</summary>
        static void Tile(string prefab, Transform parent, float x0, float z0, float size)
        {
            var corner = B2U(1f, 1f, 0f);
            Spawn(prefab, parent, new Vector3(x0 + (corner.x < 0 ? size : 0f), 0f, z0 + (corner.z < 0 ? size : 0f)), Quaternion.identity);
        }

        static void AddLight(Transform parent, string name, LightType type, Vector3 position, Quaternion rotation, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var light = go.AddComponent<Light>();
            light.type = type;
            light.range = range;
            light.intensity = intensity;
            light.color = new Color(1f, 0.86f, 0.65f);
            if (type == LightType.Spot)
                light.spotAngle = 80f;
            light.lightmapBakeType = LightmapBakeType.Mixed;
            light.shadows = LightShadows.None;
        }
    }
}
