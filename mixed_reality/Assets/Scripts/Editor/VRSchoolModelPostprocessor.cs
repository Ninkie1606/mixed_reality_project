using UnityEditor;
using UnityEngine;

namespace VRSchool.Editor
{
    /// <summary>
    /// Vaste import-instellingen voor alle FBX'en onder Assets/Art/ (zie VR School asset-plan §9),
    /// en zet UCX_-collision-meshes uit Blender om naar BoxColliders.
    /// </summary>
    class VRSchoolModelPostprocessor : AssetPostprocessor
    {
        const string ArtRoot = "Assets/Art/";
        const string CollisionPrefix = "UCX_";

        // Verhogen na elke wijziging hieronder: Unity herimporteert dan de betrokken modellen.
        public override uint GetVersion() => 2;

        bool IsSchoolModel =>
            assetPath.StartsWith(ArtRoot) && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (!IsSchoolModel)
                return;

            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;              // "Convert Units"
            importer.generateSecondaryUV = false;      // UV1 (lightmap) komt uit Blender
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!IsSchoolModel)
                return;

            // Elke UCX_-mesh wordt een onzichtbaar child met precies één BoxCollider
            // (één collider per GameObject houdt de import-identifiers uniek).
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root.transform || !t.name.StartsWith(CollisionPrefix))
                    continue;
                var filter = t.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;
                var bounds = filter.sharedMesh.bounds;
                Object.DestroyImmediate(t.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(filter);
                var box = t.gameObject.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }
        }
    }
}
