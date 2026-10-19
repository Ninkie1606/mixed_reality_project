using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkRoomMaze.EditorTools
{
    [CustomEditor(typeof(MazeGenerator))]
    public class MazeGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var gen = (MazeGenerator)target;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Genereren vervangt alles onder 'Generated'. Losse muren/lampen kun je daarna gewoon " +
                "verplaatsen of verwijderen; klik 'Vastzetten' om je handmatige aanpassingen te bewaren " +
                "voordat je opnieuw genereert.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Genereer", GUILayout.Height(28)))
                    Run(gen, () => gen.Generate());
                if (GUILayout.Button("Nieuwe seed", GUILayout.Height(28)))
                {
                    Undo.RecordObject(gen, "New maze seed");
                    gen.seed = Random.Range(0, 100000);
                    Run(gen, () => gen.Generate());
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!gen.GeneratedRoot))
                {
                    if (GUILayout.Button("Leegmaken"))
                        Run(gen, () => gen.Clear());
                    if (GUILayout.Button("Vastzetten"))
                    {
                        // hernoemen = de generator raakt dit doolhof niet meer aan
                        Undo.RecordObject(gen.GeneratedRoot.gameObject, "Lock maze");
                        gen.GeneratedRoot.name = $"Maze_seed{gen.seed}";
                        EditorSceneManager.MarkSceneDirty(gen.gameObject.scene);
                    }
                }
            }
        }

        static void Run(MazeGenerator gen, System.Action action)
        {
            Undo.IncrementCurrentGroup();
            action();
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            EditorSceneManager.MarkSceneDirty(gen.gameObject.scene);
        }
    }
}
