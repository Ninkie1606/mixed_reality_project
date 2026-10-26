using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using VRSchool.Interaction;

namespace VRSchool.Editor
{
    /// <summary>Hoe een asset als prefab wordt ingericht.</summary>
    public enum Kind
    {
        Static,             // statisch, BoxCollider per mesh
        StaticUcx,          // statisch, colliders komen van UCX_ (postprocessor)
        StaticMeshCollider, // statisch, MeshCollider + TeleportationArea (trap)
        StaticMesh,         // statisch, MeshCollider (borden met goot, rekken: props kunnen erop/erin liggen)
        Floor,              // statisch, BoxCollider + TeleportationArea
        Light,              // statisch + baked Point Light
        Door,               // SwingDoor + kinematic Rigidbody
        DoubleDoor,         // SwingDoor per blad
        Handle,             // XRSimpleInteractable
        Grab,               // Rigidbody + XRGrabInteractable, BoxCollider
        GrabRound,          // idem, CapsuleCollider langs de langste as
        Pinned,             // grab-prop die blijft hangen (kinematic), bv. briefje op prikbord
        Clock,              // statisch + WallClock, wijzers niet statisch
        LaptopOpen,         // grab, deksel open
        LaptopClosed,       // grab, deksel dicht
        ProjectorScreen,    // statisch, doek niet statisch (oprollen = schalen)
        HingedCabinet,      // kast statisch + MeshCollider (open binnenruimte), elke *_Door* child: SwingDoor + XRSimpleInteractable
                            // (lockers, microgolf, cabine, ingangsdeuren, poort); *_R = gespiegeld blad, draait de andere kant op
        StreetLight,        // statisch, BoxCollider + Point Light bovenaan (lantaarnpaal)
    }

    /// <summary>Eén asset: prefabnaam, bronmodel (FBX), categorie (submap onder Art/ en Prefabs/) en Kind.</summary>
    public readonly struct AssetDef
    {
        public readonly string Prefab;
        public readonly string Model;
        public readonly string Category;
        public readonly Kind Kind;

        public AssetDef(string prefab, string category, Kind kind, string model = null)
        {
            Prefab = prefab;
            Model = model ?? prefab;
            Category = category;
            Kind = kind;
        }

        public string ModelPath => $"Assets/Art/{Category}/{Model}.fbx";
        public string PrefabPath => $"Assets/Prefabs/{Category}/{Prefab}.prefab";
    }

    /// <summary>
    /// Gedeelde bouwstenen voor de VR School-builders: URP-materialen, material-remap, prefab-inrichting,
    /// asconversie Blender -> Unity en plaatsingshulpen voor scenes.
    /// </summary>
    public static class VRSchoolBuildCore
    {
        public const string MaterialDir = "Assets/Art/Materials";
        public const string XROriginPath = "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab";

        struct MaterialDef
        {
            public string Name;
            public Color Linear;            // zelfde (lineaire) waarden als in Blender
            public float Metallic;
            public float Roughness;
            public Color Emission;
            public float EmissionStrength;
        }

        static MaterialDef Def(string name, float r, float g, float b, float a, float metallic, float roughness,
            float er = 0f, float eg = 0f, float eb = 0f, float emissionStrength = 0f) => new MaterialDef
        {
            Name = name, Linear = new Color(r, g, b, a), Metallic = metallic, Roughness = roughness,
            Emission = new Color(er, eg, eb), EmissionStrength = emissionStrength,
        };

        // Moet overeenkomen met MATERIALS in build_modular_kit.py, build_classroom_kit.py, build_hallway_kit.py,
        // build_toilets_kit.py en build_outdoor_kit.py
        static readonly MaterialDef[] Materials =
        {
            // Fase 1
            Def("M_Wall_Paint_White", 0.82f, 0.81f, 0.77f, 1, 0, 0.9f),
            Def("M_Floor_Linoleum", 0.30f, 0.36f, 0.34f, 1, 0, 0.55f),
            Def("M_Concrete", 0.50f, 0.50f, 0.48f, 1, 0, 0.95f),
            Def("M_Wood_Light", 0.62f, 0.45f, 0.28f, 1, 0, 0.6f),
            Def("M_Metal_Painted", 0.28f, 0.35f, 0.42f, 1, 0.3f, 0.5f),
            Def("M_Metal_White", 0.88f, 0.88f, 0.86f, 1, 0.2f, 0.4f),
            Def("M_Metal_Chrome", 0.80f, 0.80f, 0.80f, 1, 1, 0.15f),
            Def("M_Glass", 0.80f, 0.90f, 0.95f, 0.2f, 0, 0.05f),
            Def("M_Plastic_White", 0.90f, 0.90f, 0.88f, 1, 0, 0.5f),
            Def("M_Rubber_Black", 0.03f, 0.03f, 0.03f, 1, 0, 0.9f),
            Def("M_Paint_Red", 0.70f, 0.03f, 0.02f, 1, 0, 0.35f),
            Def("M_Emissive_Light", 1.0f, 0.98f, 0.92f, 1, 0, 0.5f, 1.0f, 0.98f, 0.92f, 3f),
            Def("M_ExitSign", 0.05f, 0.75f, 0.25f, 1, 0, 0.4f, 0.05f, 0.9f, 0.3f, 2f),
            // Fase 2
            Def("M_Wood_Dark", 0.22f, 0.13f, 0.07f, 1, 0, 0.6f),
            Def("M_Chalkboard", 0.03f, 0.07f, 0.05f, 1, 0, 0.95f),
            Def("M_Whiteboard", 0.90f, 0.90f, 0.90f, 1, 0, 0.15f),
            Def("M_Plastic_Chair", 0.06f, 0.20f, 0.50f, 1, 0, 0.5f),
            Def("M_Plastic_Black", 0.02f, 0.02f, 0.02f, 1, 0, 0.4f),
            Def("M_Plastic_Yellow", 0.80f, 0.55f, 0.02f, 1, 0, 0.5f),
            Def("M_Plastic_Green", 0.05f, 0.35f, 0.10f, 1, 0, 0.5f),
            Def("M_Plastic_Pink", 0.90f, 0.40f, 0.45f, 1, 0, 0.7f),
            Def("M_Screen", 0.01f, 0.012f, 0.015f, 1, 0, 0.1f),
            Def("M_Paper", 0.85f, 0.85f, 0.82f, 1, 0, 0.9f),
            Def("M_Cork", 0.45f, 0.30f, 0.16f, 1, 0, 0.95f),
            Def("M_Fabric_Blue", 0.05f, 0.12f, 0.35f, 1, 0, 0.95f),
            Def("M_Fabric_Red", 0.45f, 0.04f, 0.04f, 1, 0, 0.95f),
            Def("M_Fabric_Dark", 0.03f, 0.03f, 0.035f, 1, 0, 0.95f),
            Def("M_Poster", 0.60f, 0.60f, 0.60f, 1, 0, 0.8f),
            Def("M_WorldMap", 0.15f, 0.35f, 0.60f, 1, 0, 0.8f),
            // Fase 3
            Def("M_Metal_Gold", 0.85f, 0.62f, 0.20f, 1, 1, 0.25f),
            Def("M_Soil", 0.10f, 0.06f, 0.035f, 1, 0, 1f),
            Def("M_Plant_Leaf", 0.08f, 0.30f, 0.06f, 1, 0, 0.7f),
            // Fase 4 (toiletten)
            Def("M_Ceramic_White", 0.92f, 0.92f, 0.90f, 1, 0, 0.1f),
            Def("M_Laminate_Teal", 0.10f, 0.38f, 0.40f, 1, 0, 0.4f),
            Def("M_Mirror", 0.95f, 0.95f, 0.95f, 1, 1, 0.02f),
            // Fase 5 (buiten, ingang)
            Def("M_Brick", 0.36f, 0.15f, 0.09f, 1, 0, 0.9f),
            Def("M_Glass_Dark", 0.04f, 0.06f, 0.08f, 1, 0.3f, 0.08f),
            Def("M_Paving", 0.42f, 0.42f, 0.40f, 1, 0, 0.9f),
            Def("M_Grass", 0.10f, 0.22f, 0.05f, 1, 0, 1f),
            Def("M_Plant_Conifer", 0.03f, 0.15f, 0.05f, 1, 0, 0.8f),
            Def("M_Sign_Letters", 1.0f, 0.97f, 0.90f, 1, 0, 0.5f, 1.0f, 0.95f, 0.85f, 2.5f),
        };

        // Massa (kg) voor grab-props; anders 1 kg.
        static readonly Dictionary<string, float> Masses = new()
        {
            ["SM_Build_FireExtinguisher"] = 6f, ["SM_Class_TrashBin"] = 1.5f, ["SM_Class_Laptop"] = 1.5f,
            ["SM_Class_Laptop_Closed"] = 1.5f, ["SM_Class_PaperNote"] = 0.005f,
            ["SM_Prop_Book_A"] = 0.5f, ["SM_Prop_Book_B"] = 0.4f, ["SM_Prop_Book_C"] = 0.6f, ["SM_Prop_Notebook"] = 0.2f,
            ["SM_Prop_Pen"] = 0.02f, ["SM_Prop_Pencil"] = 0.01f, ["SM_Prop_Eraser"] = 0.02f, ["SM_Prop_Ruler"] = 0.03f,
            ["SM_Prop_Calculator"] = 0.15f, ["SM_Prop_PencilCase"] = 0.25f, ["SM_Prop_Backpack_A"] = 3f,
            ["SM_Prop_Backpack_B"] = 3f, ["SM_Prop_WaterBottle"] = 0.6f, ["SM_Prop_Chalk"] = 0.01f, ["SM_Prop_Marker"] = 0.02f,
            ["SM_Prop_Trophy_A"] = 0.8f, ["SM_Prop_Trophy_B"] = 0.5f, ["SM_Prop_Tray"] = 0.4f, ["SM_Prop_LunchBox"] = 0.4f,
        };

        // ------------------------------------------------------------------ materialen

        public static void BuildMaterials(StringBuilder log)
        {
            EnsureFolder(MaterialDir);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var def in Materials)
            {
                var path = $"{MaterialDir}/{def.Name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(lit) { name = def.Name };
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = lit;
                var c = def.Linear.gamma;
                c.a = def.Linear.a;
                mat.SetColor("_BaseColor", c);
                mat.SetFloat("_Metallic", def.Metallic);
                mat.SetFloat("_Smoothness", 1f - def.Roughness);

                if (def.EmissionStrength > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", def.Emission.gamma * def.EmissionStrength);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }

                if (def.Linear.a < 1f)
                    MakeTransparent(mat);
                EditorUtility.SetDirty(mat);
            }
            log.AppendLine($"Materialen: {Materials.Length} in {MaterialDir}");
        }

        static void MakeTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetShaderPassEnabled("DepthOnly", false);
            mat.SetShaderPassEnabled("ShadowCaster", false);
        }

        public static void RemapModelMaterials(IEnumerable<AssetDef> assets, StringBuilder log)
        {
            var count = 0;
            foreach (var path in assets.Select(a => a.ModelPath).Distinct())
            {
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                {
                    log.AppendLine($"!! Model ontbreekt: {path}");
                    continue;
                }
                foreach (var def in Materials)
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{def.Name}.mat");
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), def.Name), mat);
                }
                importer.SaveAndReimport();
                count++;
            }
            log.AppendLine($"Modellen (her)geïmporteerd met materiaal-remap: {count}");
        }

        // ------------------------------------------------------------------ assen

        // Blender (x, y, z) -> Unity lokaal. Gemeten op de geïmporteerde modellen (zie DetectAxes).
        static float axisX = -1f, axisY = -1f;

        public static Vector3 B2U(float x, float y, float z) => new Vector3(axisX * x, z, axisY * y);

        /// <summary>Rotatie waarmee de voorkant van een asset (Blender -Y) naar 'direction' kijkt.</summary>
        public static Quaternion Facing(Vector3 direction) =>
            Quaternion.LookRotation(direction) * Quaternion.Inverse(Quaternion.LookRotation(B2U(0, -1, 0)));

        public static void DetectAxes(StringBuilder log)
        {
            // Wall_4m loopt in Blender langs +X; de radiator steekt uit naar -Y.
            var wall = MeasureModel("Assets/Art/Building/SM_Build_Wall_4m.fbx");
            var radiator = MeasureModel("Assets/Art/Building/SM_Build_Radiator.fbx");
            axisX = wall.center.x >= 0f ? 1f : -1f;
            axisY = radiator.center.z <= 0f ? 1f : -1f;
            log.AppendLine($"Assen: Blender X -> Unity {(axisX > 0 ? "+X" : "-X")}, Blender Y -> Unity {(axisY > 0 ? "+Z" : "-Z")}");
        }

        static Bounds MeasureModel(string path)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var b = RendererBounds(go);
            Object.DestroyImmediate(go);
            return b;
        }

        public static Bounds RendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers.Skip(1))
                b.Encapsulate(r.bounds);
            return b;
        }

        // ------------------------------------------------------------------ prefabs

        public static void BuildPrefabs(IEnumerable<AssetDef> assets, StringBuilder log)
        {
            var count = 0;
            foreach (var asset in assets)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(asset.ModelPath);
                if (model == null)
                    continue;
                EnsureFolder(System.IO.Path.GetDirectoryName(asset.PrefabPath).Replace('\\', '/'));
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                go.name = asset.Prefab;
                Configure(go, asset);
                PrefabUtility.SaveAsPrefabAsset(go, asset.PrefabPath);
                Object.DestroyImmediate(go);
                prefabCache.Remove(asset.Prefab);
                count++;
            }
            log.AppendLine($"Prefabs: {count}");
        }

        static void Configure(GameObject go, AssetDef asset)
        {
            var meshes = go.GetComponentsInChildren<MeshFilter>();
            switch (asset.Kind)
            {
                case Kind.Static:
                case Kind.Light:
                    foreach (var mf in meshes)
                        FitBox(mf);
                    SetStatic(go, go.name != "SM_Build_Window_Glass");
                    if (asset.Kind == Kind.Light)
                        AddCeilingLight(go);
                    break;

                case Kind.StaticUcx:
                    SetStatic(go, true);
                    break;

                case Kind.StaticMeshCollider:
                case Kind.StaticMesh:
                    foreach (var mf in meshes)
                        mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                    if (asset.Kind == Kind.StaticMeshCollider)
                        go.AddComponent<TeleportationArea>();
                    SetStatic(go, true);
                    break;

                case Kind.Floor:
                    foreach (var mf in meshes)
                        FitBox(mf);
                    go.AddComponent<TeleportationArea>();
                    SetStatic(go, true);
                    break;

                case Kind.Handle:
                    foreach (var mf in meshes)
                        FitBox(mf);
                    go.AddComponent<XRSimpleInteractable>();
                    break;

                case Kind.Door:
                    foreach (var mf in meshes)
                        FitBox(mf);
                    go.AddComponent<Rigidbody>().isKinematic = true;
                    if (go.name == "SM_Build_Door_Classroom")
                    {
                        // Klink langs beide kanten, 7 cm van de vrije rand, op 1,05 m.
                        AddChildPrefab(go.transform, "SM_Build_DoorHandle", "Handle_Front", B2U(0.82f, -0.02f, 1.05f), Quaternion.identity);
                        AddChildPrefab(go.transform, "SM_Build_DoorHandle", "Handle_Back", B2U(0.82f, 0.02f, 1.05f), Quaternion.Euler(180f, 0f, 0f));
                    }
                    else
                    {
                        go.AddComponent<XRSimpleInteractable>();   // paniekbalk / blad zelf
                    }
                    // Klasdeur draait naar de kamerzijde (Blender -Y), nooddeur naar buiten.
                    var swing = go.AddComponent<SwingDoor>();
                    if (go.name == "SM_Build_Door_Emergency")
                        SetOpenAngle(swing, -95f);
                    break;

                case Kind.DoubleDoor:
                    foreach (var mf in meshes)
                    {
                        FitBox(mf);
                        var leaf = mf.gameObject;
                        leaf.AddComponent<Rigidbody>().isKinematic = true;
                        leaf.AddComponent<XRSimpleInteractable>();
                        var door = leaf.AddComponent<SwingDoor>();
                        if (leaf.name.EndsWith("_R"))
                            SetOpenAngle(door, -95f);
                    }
                    break;

                case Kind.Grab:
                case Kind.Pinned:
                    foreach (var mf in meshes)
                        FitBox(mf);
                    MakeGrabbable(go, asset.Kind == Kind.Pinned);
                    break;

                case Kind.GrabRound:
                    FitCapsule(go);
                    MakeGrabbable(go, false);
                    break;

                case Kind.LaptopOpen:
                case Kind.LaptopClosed:
                    if (asset.Kind == Kind.LaptopOpen)
                        OpenLid(go);
                    foreach (var mf in meshes)
                        FitBox(mf);
                    MakeGrabbable(go, false);
                    break;

                case Kind.Clock:
                    foreach (var mf in meshes.Where(m => !m.name.Contains("Hand")))
                        FitBox(mf);
                    SetStatic(go, false);
                    foreach (var hand in go.GetComponentsInChildren<Transform>().Where(t => t.name.Contains("Hand")))
                        GameObjectUtility.SetStaticEditorFlags(hand.gameObject, 0);
                    go.AddComponent<WallClock>();
                    break;

                case Kind.ProjectorScreen:
                    foreach (var mf in meshes)
                        FitBox(mf);
                    SetStatic(go, false);
                    foreach (var cloth in go.GetComponentsInChildren<Transform>().Where(t => t.name.EndsWith("_Cloth")))
                        GameObjectUtility.SetStaticEditorFlags(cloth.gameObject, 0);
                    break;

                case Kind.HingedCabinet:
                    SetStatic(go, true);
                    foreach (var mf in meshes)
                    {
                        if (!mf.name.Contains("_Door"))
                        {
                            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                            continue;
                        }
                        // Deur: zelfde opbouw als de klasdeur (blad loopt langs Blender +X) -> +95° opent naar voren.
                        var leaf = mf.gameObject;
                        GameObjectUtility.SetStaticEditorFlags(leaf, 0);
                        FitBox(mf);
                        leaf.AddComponent<Rigidbody>().isKinematic = true;
                        leaf.AddComponent<XRSimpleInteractable>();
                        var hinged = leaf.AddComponent<SwingDoor>();
                        if (leaf.name.EndsWith("_R"))
                            SetOpenAngle(hinged, -95f);
                    }
                    break;

                case Kind.StreetLight:
                    foreach (var mf in meshes)
                        FitBox(mf);
                    SetStatic(go, true);
                    AddStreetLight(go);
                    break;
            }
        }

        static void MakeGrabbable(GameObject go, bool pinned)
        {
            var body = go.AddComponent<Rigidbody>();
            body.mass = Masses.TryGetValue(go.name, out var m) ? m : 1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = pinned;
            go.AddComponent<XRGrabInteractable>();
        }

        static void OpenLid(GameObject go)
        {
            // Draairichting meten i.p.v. raden: het deksel moet na het openen boven de basis uitsteken.
            var lid = go.GetComponentsInChildren<Transform>().First(t => t.name.EndsWith("_Lid"));
            var closed = lid.localRotation;
            foreach (var angle in new[] { 110f, -110f })
            {
                lid.localRotation = closed * Quaternion.AngleAxis(angle, Vector3.right);
                if (RendererBounds(lid.gameObject).max.y - go.transform.position.y > 0.15f)
                    return;
            }
        }

        static void SetOpenAngle(SwingDoor door, float angle)
        {
            var so = new SerializedObject(door);
            so.FindProperty("openAngle").floatValue = angle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void FitBox(MeshFilter mf)
        {
            var box = mf.gameObject.AddComponent<BoxCollider>();
            box.center = mf.sharedMesh.bounds.center;
            box.size = mf.sharedMesh.bounds.size;
        }

        static void FitCapsule(GameObject go)
        {
            var mesh = go.GetComponentInChildren<MeshFilter>().sharedMesh;
            var b = mesh.bounds;
            var size = b.size;
            var axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = axis;
            capsule.center = b.center;
            capsule.height = size[axis];
            capsule.radius = Mathf.Max(size[(axis + 1) % 3], size[(axis + 2) % 3]) / 2f;
        }

        static void SetStatic(GameObject go, bool occluder)
        {
            var flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
                        StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
            if (occluder)
                flags |= StaticEditorFlags.OccluderStatic;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }

        static void AddCeilingLight(GameObject go)
        {
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = B2U(0f, 0f, -0.25f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 5f;
            light.intensity = 1.5f;
            light.color = new Color(1f, 0.97f, 0.9f);
            light.lightmapBakeType = LightmapBakeType.Baked;
            light.shadows = LightShadows.Soft;
        }

        static void AddStreetLight(GameObject go)
        {
            // Mixed: werkt realtime zolang er niet gebakken is, daarna baked indirect licht.
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = B2U(0f, 0f, 3.75f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 7f;
            light.intensity = 2f;
            light.color = new Color(1f, 0.85f, 0.65f);
            light.lightmapBakeType = LightmapBakeType.Mixed;
            light.shadows = LightShadows.None;
        }

        static void AddChildPrefab(Transform parent, string prefabName, string childName, Vector3 localPos, Quaternion localRot)
        {
            var child = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(prefabName));
            child.name = childName;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPos;
            child.transform.localRotation = localRot;
        }

        // ------------------------------------------------------------------ plaatsing in scenes

        /// <summary>Plaatst muurstukken achter elkaar van start langs dir; de kamerzijde (Blender -Y) kijkt naar inward.</summary>
        public static List<(string name, Vector3 pivot, Quaternion rot)> Run(Transform parent, Vector3 start, Vector3 dir,
            Vector3 inward, params string[] pieces)
        {
            var result = new List<(string, Vector3, Quaternion)>();
            var rot = Facing(inward);
            var along = rot * B2U(1, 0, 0);
            var pos = start;
            foreach (var piece in pieces)
            {
                var length = piece.Contains("8m") ? 8f : piece.Contains("4m") || piece.EndsWith("Door_Double") ? 4f : 2f;
                // Lokale Blender-X van het stuk wijst langs 'along'; pivot aan het juiste uiteinde zetten.
                var pivot = Vector3.Dot(along, dir) > 0f ? pos : pos + dir * length;
                Spawn(piece, parent, pivot, rot);
                result.Add((piece, pivot, rot));
                pos += dir * length;
            }
            return result;
        }

        public static GameObject SpawnLocal(string prefab, Transform parent, (string name, Vector3 pivot, Quaternion rot) wall,
            float bx, float by, float bz)
            => Spawn(prefab, parent, wall.pivot + wall.rot * B2U(bx, by, bz), wall.rot);

        public static GameObject Spawn(string prefab, Transform parent, Vector3 position, Quaternion rotation)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(prefab), parent);
            go.transform.SetPositionAndRotation(position, rotation);
            return go;
        }

        /// <summary>Zet alle opgegeven prefabs op een rij (vanaf 'origin' langs +X) naast een referentiecapsule van 1,8 m.</summary>
        public static void BuildShowcase(Vector3 origin, IEnumerable<string> prefabs)
        {
            var showcase = new GameObject("Showcase").transform;
            showcase.position = origin;
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Ref_Capsule_1.8m";
            Object.DestroyImmediate(capsule.GetComponent<Collider>());
            capsule.transform.SetParent(showcase, false);
            capsule.transform.localPosition = new Vector3(-1f, 0.9f, 0f);
            capsule.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            var cursor = origin.x;
            foreach (var name in prefabs)
            {
                var go = Spawn(name, showcase, Vector3.zero, Quaternion.identity);
                foreach (var body in go.GetComponentsInChildren<Rigidbody>())
                    body.isKinematic = true;   // showcase blijft staan
                var b = RendererBounds(go);
                go.transform.position += new Vector3(cursor - b.min.x, origin.y, origin.z - b.center.z);
                cursor += b.size.x + 0.4f;
            }
        }

        public static void PlaceXROrigin(UnityEngine.SceneManagement.Scene scene, Vector3 position, Quaternion rotation, StringBuilder log)
        {
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPath);
            if (rig == null)
            {
                log.AppendLine($"!! XR Origin niet gevonden op {XROriginPath}");
                return;
            }
            var rigGo = (GameObject)PrefabUtility.InstantiatePrefab(rig, scene);
            rigGo.transform.SetPositionAndRotation(position, rotation);
            var cam = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Main Camera");
            if (cam != null)
                Object.DestroyImmediate(cam);
            log.AppendLine("XR Origin geplaatst (Complete XR Origin Set Up Variant)");
        }

        // ------------------------------------------------------------------ helpers

        static readonly Dictionary<string, GameObject> prefabCache = new();

        public static GameObject LoadPrefab(string name)
        {
            if (prefabCache.TryGetValue(name, out var cached) && cached != null)
                return cached;
            var path = AssetDatabase.FindAssets($"{name} t:Prefab", new[] { "Assets/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == name);
            var prefab = path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new System.ArgumentException($"Prefab niet gevonden: {name}");
            return prefabCache[name] = prefab;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
