using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VRSchool.Visual;

namespace VRSchool.Editor
{
    /// <summary>
    /// Hoofdscene: zet de bestaande scenes Entrance_01, Hallway_01 en Classroom_01 achter elkaar in Assets/Scenes/Main.unity.
    /// Ingang (z = 16) -> inkomhal (3 m) -> doorgang in de achterwand -> dubbele deur van de gang (gang loopt naar het noorden)
    /// -> tweede klasdeur van de gang -> klaslokaal. Showcase-rijen, losse lichten en XR Origins van gang en klas gaan niet mee;
    /// de speler start aan de schoolpoort (XR Origin van Entrance_01).
    /// Vereist dat Fase 1, 2, 3 en 5 gebouwd zijn. Opnieuw uitvoeren is veilig (de bronscenes blijven ongewijzigd).
    /// </summary>
    public static class VRSchoolMainSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string EntrancePath = "Assets/Scenes/Entrance_01.unity";
        const string HallwayPath = "Assets/Scenes/Hallway_01.unity";
        const string ClassroomPath = "Assets/Scenes/Classroom_01.unity";

        // Achterwand van de inkomhal: gevel-gridlijn z = 16 + halve geveldikte 0,15 + diepte 3 + wanddikte 0,05.
        // De westmuur van de gang (0,2 m dik, gecentreerd op zijn gridlijn) sluit daar aan.
        const float LobbyBackZ = 16f + 0.15f + 3f + 0.05f;
        const float HallwayWallZ = LobbyBackZ + 0.1f;
        const float HallwayDepth = 4f;

        [MenuItem("VR School/Build Main Scene")]
        public static void BuildAllMenu() => Debug.Log(BuildAll());

        public static string BuildAll()
        {
            var log = new StringBuilder();
            foreach (var path in new[] { EntrancePath, HallwayPath, ClassroomPath })
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    return $"!! {path} ontbreekt: bouw eerst die fase (menu VR School).";

            if (SceneManager.GetActiveScene().path == ScenePath)
                return $"!! {ScenePath} is open: open eerst een andere scene en voer dit opnieuw uit.";

            var previousActive = SceneManager.GetActiveScene();
            var main = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                // --- Ingang: alles behalve de showcase (zon, focusvolume, XR Origin aan de poort gaan mee)
                var entrance = EditorSceneManager.OpenScene(EntrancePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(entrance);
                var fog = (RenderSettings.fog, RenderSettings.fogMode, RenderSettings.fogColor,
                           RenderSettings.fogStartDistance, RenderSettings.fogEndDistance);
                foreach (var go in entrance.GetRootGameObjects().Where(g => g.name != "Showcase"))
                    SceneManager.MoveGameObjectToScene(go, main);
                EditorSceneManager.CloseScene(entrance, true);

                SceneManager.SetActiveScene(main);
                (RenderSettings.fog, RenderSettings.fogMode, RenderSettings.fogColor,
                 RenderSettings.fogStartDistance, RenderSettings.fogEndDistance) = fog;

                // --- Gang: lokale +X (lengte) wijst naar het noorden, westmuur met dubbele deur sluit aan op de inkomhal
                var hallway = MoveRoot(HallwayPath, "Hallway_01", main);
                hallway.SetPositionAndRotation(new Vector3(HallwayDepth / 2f, 0f, HallwayWallZ), Quaternion.Euler(0f, -90f, 0f));

                // --- Klas: oostmuur valt samen met de noordmuur van de gang, deur op de tweede klasdeur van de gang
                var classroom = MoveRoot(ClassroomPath, "Classroom_01", main);
                ConnectClassroom(hallway, classroom, log);

                AddInteriorZone(main, hallway, classroom, log);
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

        static Transform MoveRoot(string scenePath, string rootName, Scene target)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var root = scene.GetRootGameObjects().First(g => g.name == rootName);
            SceneManager.MoveGameObjectToScene(root, target);
            EditorSceneManager.CloseScene(scene, true);
            return root.transform;
        }

        static void ConnectClassroom(Transform hallway, Transform classroom, StringBuilder log)
        {
            // klas lokaal -X (kamerzijde van de oostmuur) = gang lokaal +Z (achter de noordmuur)
            classroom.rotation = hallway.rotation * Quaternion.Euler(0f, 90f, 0f);
            classroom.position = Vector3.zero;

            // tweede klasdeur in de noordmuur (lokaal z = HallwayDepth); de nooddeur in de oostmuur gebruikt hetzelfde muurstuk
            var hallDoorWall = WallPieces(hallway, "SM_Build_Wall_Door")
                .Where(w => hallway.InverseTransformPoint(Center(w)).z > HallwayDepth - 0.5f)
                .OrderByDescending(w => hallway.InverseTransformPoint(Center(w)).x).First();
            var classDoorWall = WallPieces(classroom, "SM_Build_Wall_Door").First();
            var delta = Center(hallDoorWall) - Center(classDoorWall);
            classroom.position += new Vector3(delta.x, 0f, delta.z);

            // De gangmuur dient als oostmuur van de klas: dubbele muurstukken, hoekkolommen en de klasdeur weg.
            var gridX = classroom.InverseTransformPoint(Center(classDoorWall)).x;
            var removed = 0;
            foreach (var t in classroom.GetComponentsInChildren<Transform>(true)
                         .Where(t => t.parent != null && t.parent.parent == classroom && IsOnEastLine(t))
                         .ToArray())
            {
                Object.DestroyImmediate(t.gameObject);
                removed++;
            }
            log.AppendLine($"Klas gekoppeld aan de gang ({removed} dubbele stukken op de gedeelde muur verwijderd)");

            bool IsOnEastLine(Transform t)
            {
                var n = t.name;
                if (!(n.StartsWith("SM_Build_Wall") || n.StartsWith("SM_Build_Door_Classroom")))
                    return false;
                var c = classroom.InverseTransformPoint(Center(t));
                return Mathf.Abs(c.x - gridX) < 0.35f;
            }
        }

        static Transform[] WallPieces(Transform root, string prefab) =>
            root.GetComponentsInChildren<Transform>(true).Where(t => t.name == prefab).ToArray();

        static Vector3 Center(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return t.position;
            var b = renderers[0].bounds;
            foreach (var r in renderers.Skip(1))
                b.Encapsulate(r.bounds);
            return b.center;
        }

        static void AddInteriorZone(Scene main, Transform hallway, Transform classroom, StringBuilder log)
        {
            var bounds = new Bounds(hallway.position, Vector3.zero);
            foreach (var r in hallway.GetComponentsInChildren<Renderer>().Concat(classroom.GetComponentsInChildren<Renderer>()))
                bounds.Encapsulate(r.bounds);
            bounds.min = new Vector3(bounds.min.x, bounds.min.y, LobbyBackZ);   // begint net achter de inkomhal

            var go = new GameObject("InteriorZone");
            SceneManager.MoveGameObjectToScene(go, main);
            go.transform.position = bounds.center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = bounds.size;

            var focus = main.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FocusOnTarget>(true)).FirstOrDefault();
            var zone = new SerializedObject(go.AddComponent<InteriorZone>());
            zone.FindProperty("outdoorFocus").objectReferenceValue = focus;
            zone.FindProperty("outdoorVolume").objectReferenceValue = focus != null ? focus.GetComponent<Volume>() : null;
            zone.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine($"InteriorZone {bounds.size:F1} m (focus {(focus != null ? "gekoppeld" : "NIET gevonden")})");
        }

        static void AddToBuildSettings(StringBuilder log)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            log.AppendLine("Main staat op index 0 in de Build Settings");
        }
    }
}
