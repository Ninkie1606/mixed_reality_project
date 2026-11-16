using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace VRSchool.Editor
{
    /// <summary>
    /// Controls-graffiti (uitleg voor nieuwe spelers) op de buitenkant van de westmuur in Test_Modular.
    /// Texture en FBX komen uit blender/tools/build_graffiti_controls.py. Opnieuw uitvoeren is veilig;
    /// na "Build Phase 1 - Modular Kit" (nieuwe testscene) dit menu opnieuw draaien.
    /// </summary>
    public static class VRSchoolGraffitiBuilder
    {
        const string Asset = "SM_Deco_Graffiti_Controls";
        const string ModelPath = "Assets/Art/Props/" + Asset + ".fbx";
        const string TexturePath = "Assets/Art/Textures/T_Graffiti_Controls_BaseColor.png";
        const string MaterialPath = "Assets/Art/Materials/M_Graffiti_Controls.mat";
        const string ConcretePath = "Assets/Art/Materials/M_Concrete.mat";
        const string PrefabDir = "Assets/Prefabs/Props";
        const string ScenePath = "Assets/Scenes/Test_Modular.unity";
        const string RootName = "Outside_Graffiti";
        const float HeightAboveGround = 0.6f;

        [MenuItem("VR School/Place Controls Graffiti")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            var log = new StringBuilder("[VRSchoolGraffitiBuilder]\n");
            SetupTexture();
            var mat = SetupMaterial();
            SetupModel(mat);
            var prefab = BuildPrefab();
            PlaceInScene(prefab, log);
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        static void SetupTexture()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 4096;            // tekst moet scherp blijven op een paar meter
            importer.anisoLevel = 8;                   // scherp onder een schuine kijkhoek
            importer.filterMode = FilterMode.Trilinear;
            importer.mipMapBias = -0.5f;               // iets scherpere mips op afstand
            importer.SaveAndReimport();
        }

        static Material SetupMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Graffiti_Controls" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_Metallic", 0f);
            // Transparante verf op de muur (zelfde instellingen als M_Glass in VRSchoolKitBuilder)
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            // Geen reflecties/specular waar alpha 0 is (anders blijft een lichte rechthoek zichtbaar)
            mat.SetFloat("_BlendModePreserveSpecular", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetShaderPassEnabled("DepthOnly", false);
            mat.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void SetupModel(Material mat)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mat.name), mat);
            importer.SaveAndReimport();
        }

        static GameObject BuildPrefab()
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Props");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            go.name = Asset;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                r.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{Asset}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void PlaceInScene(GameObject prefab, StringBuilder log)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RootName))
                Object.DestroyImmediate(old);

            // Westmuur = het 4 m-stuk dat langs Z loopt met het kleinste X; de buitenkant is bounds.min.x.
            var wall = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name == "SM_Build_Wall_4m" && t.parent != null && t.parent.name == "Walls")
                .Select(t => Bounds(t.gameObject))
                .Where(b => b.size.z > b.size.x)
                .OrderBy(b => b.center.x)
                .First();

            var root = new GameObject(RootName).transform;
            var graffiti = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            // Quad kijkt in Unity naar lokaal +Z (Blender -Y) -> naar buiten (-X) draaien.
            graffiti.transform.SetPositionAndRotation(
                new Vector3(wall.min.x, HeightAboveGround, wall.center.z), Quaternion.LookRotation(Vector3.left));

            // Stukje buitengrond zodat de muur niet in het niets staat
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Outside_Ground";
            ground.transform.SetParent(root, false);
            ground.transform.position = new Vector3(wall.min.x - 3f, -0.01f, 3f);
            ground.transform.localScale = new Vector3(0.6f, 1f, 0.8f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ConcretePath);
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);

            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"Graffiti op westmuur (buitenkant x = {wall.min.x:0.00}, z = {wall.center.z:0.00}) in {ScenePath}");
        }

        static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers.Skip(1))
                b.Encapsulate(r.bounds);
            return b;
        }
    }
}
