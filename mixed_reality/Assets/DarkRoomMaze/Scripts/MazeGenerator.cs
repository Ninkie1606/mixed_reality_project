using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DarkRoomMaze
{
    /// <summary>
    /// Bouwt een doolhof uit losse prefabs (muur, vloer, plafond, lamp, uitgang).
    /// Alles komt onder het child-object "Generated" te staan; opnieuw genereren vervangt alleen dat.
    /// Pas een prefab aan en alle muren/lampen in de scene veranderen mee.
    /// </summary>
    public class MazeGenerator : MonoBehaviour
    {
        public const string GeneratedName = "Generated";

        [Header("Afmetingen (meter)")]
        [Min(2)] public int cellsX = 10;
        [Min(2)] public int cellsZ = 10;
        [Min(0.5f)] public float cellSize = 2f;
        [Min(0.5f)] public float wallHeight = 2.6f;
        [Min(0.01f)] public float wallThickness = 0.15f;

        [Header("Doolhof")]
        [Tooltip("Zelfde seed = zelfde doolhof.")]
        public int seed = 42;
        [Tooltip("Muren, vloer en plafond schalen naar de afmetingen hierboven. " +
                 "Uit = de prefab wordt op ware grootte geplaatst (handig voor een eigen muur-model van precies 1 cel breed).")]
        public bool fitToCellSize = true;
        public bool addCeiling = true;
        [Min(0)] public int lampCount = 4;
        [Tooltip("Hoogte van de lampen onder het plafond.")]
        [Min(0f)] public float lampDropFromCeiling = 0.3f;

        [Header("Prefabs")]
        public GameObject wallPrefab;
        public GameObject floorPrefab;
        public GameObject ceilingPrefab;
        public GameObject lampPrefab;
        public GameObject exitPrefab;

        [Header("Speler")]
        [Tooltip("Wordt bij genereren naar de ingang verplaatst (bv. de XR Origin).")]
        public Transform player;

        [Flags]
        enum Dir { None = 0, N = 1, S = 2, E = 4, W = 8 }

        public Transform GeneratedRoot => transform.Find(GeneratedName);

        public void Generate()
        {
            Clear();

            var rng = new System.Random(seed);
            var open = BuildLayout(rng);
            var entrance = new Vector2Int(0, 0);
            var exit = new Vector2Int(cellsX - 1, cellsZ - 1);
            float cs = cellSize, h = wallHeight, t = wallThickness;
            float width = cellsX * cs, depth = cellsZ * cs;

            var root = new GameObject(GeneratedName).transform;
            root.SetParent(transform, false);
            var walls = Group(root, "Walls");
            var lamps = Group(root, "Lamps");
            var shell = Group(root, "FloorAndCeiling");

            // muren: per cel de zuid- en westmuur, plus de noord- en oostrand
            var along = Quaternion.identity;
            var across = Quaternion.Euler(0f, 90f, 0f);
            for (int x = 0; x < cellsX; x++)
            for (int z = 0; z < cellsZ; z++)
            {
                float cx = x * cs, cz = z * cs;
                if (!open[x, z].HasFlag(Dir.S) && new Vector2Int(x, z) != entrance)
                    PlaceWall(walls, $"Wall_{x}_{z}_S", new Vector3(cx + cs / 2, 0, cz), along);
                if (!open[x, z].HasFlag(Dir.W))
                    PlaceWall(walls, $"Wall_{x}_{z}_W", new Vector3(cx, 0, cz + cs / 2), across);
            }
            for (int x = 0; x < cellsX; x++)
                if (x != exit.x)
                    PlaceWall(walls, $"Wall_{x}_{cellsZ - 1}_N", new Vector3(x * cs + cs / 2, 0, depth), along);
            for (int z = 0; z < cellsZ; z++)
                PlaceWall(walls, $"Wall_{cellsX - 1}_{z}_E", new Vector3(width, 0, z * cs + cs / 2), across);

            // vloer en plafond (1 m extra rondom, 2 m voor in- en uitgang)
            var center = new Vector3(width / 2, 0, depth / 2);
            var floorSize = new Vector3(width + 2, 0.1f, depth + 4);
            Fit(Place(floorPrefab, shell, "Floor", center, along), floorSize);
            if (addCeiling)
                Fit(Place(ceilingPrefab, shell, "Ceiling", center + Vector3.up * h, along), floorSize);

            // uitgang
            Place(exitPrefab, root, "Exit", new Vector3(exit.x * cs + cs / 2, 0, depth + 0.65f), along);

            // lampen in willekeurige doodlopende gangen
            var deadEnds = new List<Vector2Int>();
            for (int x = 0; x < cellsX; x++)
            for (int z = 0; z < cellsZ; z++)
            {
                var c = new Vector2Int(x, z);
                if (IsSingle(open[x, z]) && c != entrance && c != exit)
                    deadEnds.Add(c);
            }
            for (int i = deadEnds.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (deadEnds[i], deadEnds[j]) = (deadEnds[j], deadEnds[i]);
            }
            for (int i = 0; i < Mathf.Min(lampCount, deadEnds.Count); i++)
            {
                var c = deadEnds[i];
                Place(lampPrefab, lamps, $"Lamp_{c.x}_{c.y}",
                      new Vector3((c.x + 0.5f) * cs, h - lampDropFromCeiling, (c.y + 0.5f) * cs), along);
            }

            // startpunt voor de ingang
            var start = new GameObject("PlayerStart").transform;
            start.SetParent(root, false);
            start.localPosition = new Vector3(cs / 2, 0, -1f);
            if (player)
            {
#if UNITY_EDITOR
                Undo.RecordObject(player, "Move player to maze start");
#endif
                player.SetPositionAndRotation(start.position, start.rotation);
            }

#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Generate maze");
#endif
            Debug.Log($"Doolhof klaar: {cellsX}x{cellsZ} cellen, {width:0}x{depth:0} m, " +
                      $"{walls.childCount} muren, {lamps.childCount} lampen (seed {seed}).", this);
        }

        public void Clear()
        {
            var existing = GeneratedRoot;
            if (!existing) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                return;
            }
#endif
            Destroy(existing.gameObject);
        }

        // ---------- doolhof (recursive backtracker), zelfde algoritme als het Blender-script ----------
        Dir[,] BuildLayout(System.Random rng)
        {
            var open = new Dir[cellsX, cellsZ];
            var visited = new bool[cellsX, cellsZ];
            var stack = new Stack<Vector2Int>();
            stack.Push(Vector2Int.zero);
            visited[0, 0] = true;
            var options = new List<(Dir dir, Vector2Int next)>(4);

            while (stack.Count > 0)
            {
                var c = stack.Peek();
                options.Clear();
                TryAdd(options, visited, c, Dir.N, Vector2Int.up);
                TryAdd(options, visited, c, Dir.S, Vector2Int.down);
                TryAdd(options, visited, c, Dir.E, Vector2Int.right);
                TryAdd(options, visited, c, Dir.W, Vector2Int.left);
                if (options.Count == 0)
                {
                    stack.Pop();
                    continue;
                }
                var (dir, next) = options[rng.Next(options.Count)];
                open[c.x, c.y] |= dir;
                open[next.x, next.y] |= Opposite(dir);
                visited[next.x, next.y] = true;
                stack.Push(next);
            }
            return open;
        }

        void TryAdd(List<(Dir, Vector2Int)> options, bool[,] visited, Vector2Int c, Dir dir, Vector2Int step)
        {
            var n = c + step;
            if (n.x >= 0 && n.x < cellsX && n.y >= 0 && n.y < cellsZ && !visited[n.x, n.y])
                options.Add((dir, n));
        }

        static Dir Opposite(Dir d) => d switch
        {
            Dir.N => Dir.S,
            Dir.S => Dir.N,
            Dir.E => Dir.W,
            _ => Dir.E,
        };

        static bool IsSingle(Dir d) => d == Dir.N || d == Dir.S || d == Dir.E || d == Dir.W;

        // ---------- plaatsen ----------
        void PlaceWall(Transform parent, string name, Vector3 pos, Quaternion rot)
        {
            Fit(Place(wallPrefab, parent, name, pos, rot), new Vector3(cellSize + wallThickness, wallHeight, wallThickness));
        }

        void Fit(GameObject go, Vector3 size)
        {
            if (go && fitToCellSize) go.transform.localScale = size;
        }

        GameObject Place(GameObject prefab, Transform parent, string name, Vector3 localPos, Quaternion localRot)
        {
            if (!prefab)
            {
                Debug.LogWarning($"MazeGenerator: geen prefab ingesteld voor '{name}', overgeslagen.", this);
                return null;
            }
#if UNITY_EDITOR
            // InstantiatePrefab houdt de koppeling met de prefab, zodat aanpassingen aan de prefab doorwerken
            var go = Application.isPlaying
                ? Instantiate(prefab, parent)
                : (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
#else
            var go = Instantiate(prefab, parent);
#endif
            go.name = name;
            go.transform.SetLocalPositionAndRotation(localPos, localRot);
            return go;
        }

        static Transform Group(Transform parent, string name)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            return g;
        }
    }
}
