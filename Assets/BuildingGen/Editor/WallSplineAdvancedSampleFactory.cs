using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emadgh.BuildingGen.Editor
{
    internal static class WallSplineAdvancedSampleFactory
    {
        private const string SampleRoot = "Assets/BuildingGen/Samples/Generated";

        [MenuItem("Tools/BuildingGen/Create Advanced Wall Spline Demo")]
        public static void CreateAdvancedWallSplineDemo()
        {
            EnsureFolders();

            Material mud = GetOrCreateMaterial("MAT_WallSpline_AdvancedMud", new Color(0.66f, 0.49f, 0.31f));
            Material brick = GetOrCreateMaterial("MAT_WallSpline_AdvancedBrick", new Color(0.47f, 0.27f, 0.15f));
            Material shadow = GetOrCreateMaterial("MAT_WallSpline_AdvancedShadow", new Color(0.09f, 0.07f, 0.05f));
            Material groundMaterial = GetOrCreateMaterial("MAT_WallSpline_DemoGround", new Color(0.42f, 0.34f, 0.22f));

            GameObject crown = CreateSimplePrefab("PF_WallTop_AdvancedBrick", brick, new Vector3(0.70f, 0.18f, 0.52f));
            GameObject pillar = CreatePillarPrefab(mud, brick);
            GameObject buttress = CreateButtressPrefab(mud);
            GameObject niche = CreateNichePrefab(mud, shadow);
            GameObject endCap = CreateEndCapPrefab(mud);
            GameObject gate = CreateGatePrefab(mud, shadow);

            string stylePath = SampleRoot + "/WallStyle_YazdAdvanced.asset";
            WallSplineStyle style = AssetDatabase.LoadAssetAtPath<WallSplineStyle>(stylePath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<WallSplineStyle>();
                style.name = "WallStyle_YazdAdvanced";
                AssetDatabase.CreateAsset(style, stylePath);
            }

            style.construction = WallSplineConstruction.RuinedMud;
            style.wallMaterial = mud;
            style.wallHeight = 2.55f;
            style.wallThickness = 0.48f;
            style.chunkLength = 0.72f;
            style.heightVariation = 0.42f;
            style.thicknessVariation = 0.07f;
            style.missingChunkChance = 0.035f;
            style.baseVerticalJitter = 0.025f;
            style.yawJitter = 2.4f;

            style.generateTopPrefabs = true;
            style.topPrefabs.Clear();
            style.topPrefabs.Add(new WallTopPrefabOption { prefab = crown, weight = 1f });
            style.topSpacing = 1.15f;
            style.topSpawnChance = 0.82f;
            style.topYawJitter = 5f;
            style.topScaleRange = new Vector2(0.90f, 1.08f);

            style.generatePillars = true;
            style.pillarPrefabs.Clear();
            style.pillarPrefabs.Add(new WallTopPrefabOption { prefab = pillar, weight = 1f });
            style.pillarSpacing = 3.6f;
            style.pillarSpawnChance = 0.92f;

            style.generateButtresses = true;
            style.buttressPrefabs.Clear();
            style.buttressPrefabs.Add(new WallTopPrefabOption { prefab = buttress, weight = 1f });
            style.buttressSpacing = 5.4f;
            style.buttressSpawnChance = 0.65f;
            style.buttressOutwardOffset = 0.20f;

            style.generateNiches = true;
            style.nichePrefabs.Clear();
            style.nichePrefabs.Add(new WallTopPrefabOption { prefab = niche, weight = 1f });
            style.nicheSpacing = 4.1f;
            style.nicheSpawnChance = 0.42f;
            style.nicheHeight = 1.05f;

            style.generateEndCaps = true;
            style.endCapPrefabs.Clear();
            style.endCapPrefabs.Add(new WallTopPrefabOption { prefab = endCap, weight = 1f });
            EditorUtility.SetDirty(style);

            DestroyExisting("WallSpline_AdvancedTerrainDemo");
            DestroyExisting("WallSpline_AdvancedGround");

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "WallSpline_AdvancedGround";
            ground.transform.position = new Vector3(25f, -0.65f, 0f);
            ground.transform.rotation = Quaternion.Euler(0f, 0f, 5.5f);
            ground.transform.localScale = new Vector3(18f, 0.65f, 8f);
            MeshRenderer groundRenderer = ground.GetComponent<MeshRenderer>();
            if (groundRenderer != null)
                groundRenderer.sharedMaterial = groundMaterial;

            GameObject go = new GameObject("WallSpline_AdvancedTerrainDemo");
            Undo.RegisterCreatedObjectUndo(go, "Create advanced wall spline demo");
            go.transform.position = new Vector3(19f, 0f, -1.2f);

            WallSpline spline = go.AddComponent<WallSpline>();
            spline.style = style;
            spline.interpolation = WallSplineInterpolation.SmoothCatmullRom;
            spline.smoothSubdivisions = 6;
            spline.seed = 2505;
            spline.conformToTerrain = true;
            spline.terrainLayerMask = -1;
            spline.terrainRayHeight = 12f;
            spline.terrainRayDistance = 40f;
            spline.terrainOffset = 0.02f;
            spline.flipDecorationSide = false;
            spline.controlPoints.Clear();
            spline.controlPoints.Add(new Vector3(0f, 4f, 0f));
            spline.controlPoints.Add(new Vector3(3.4f, 4f, 0.4f));
            spline.controlPoints.Add(new Vector3(6.7f, 4f, 1.45f));
            spline.controlPoints.Add(new Vector3(10.2f, 4f, 0.3f));
            spline.controlPoints.Add(new Vector3(13.7f, 4f, 1.0f));

            spline.openings.Clear();
            spline.openings.Add(new WallSplineOpening
            {
                enabled = true,
                distance = 6.6f,
                width = 2.15f,
                prefab = gate,
                verticalOffset = 0f
            });

            WallSplineBuilder.Generate(spline);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = go;

            Debug.Log("BuildingGen: advanced WallSpline demo created with terrain conform, gate opening, crown pieces, pillars, buttresses, niches and end caps.", go);
        }

        private static GameObject CreateSimplePrefab(string name, Material material, Vector3 scale)
        {
            string path = SampleRoot + "/" + name + ".prefab";
            DeleteExistingAsset(path);
            GameObject root = new GameObject(name);
            CreateCube("Visual", root.transform, new Vector3(0f, scale.y * 0.5f, 0f), scale, material);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreatePillarPrefab(Material mud, Material brick)
        {
            const string name = "PF_WallPillar_Simple";
            string path = SampleRoot + "/" + name + ".prefab";
            DeleteExistingAsset(path);
            GameObject root = new GameObject(name);
            CreateCube("Pillar", root.transform, new Vector3(0f, 1.35f, 0f), new Vector3(0.62f, 2.7f, 0.62f), mud);
            CreateCube("Cap", root.transform, new Vector3(0f, 2.78f, 0f), new Vector3(0.78f, 0.18f, 0.78f), brick);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateButtressPrefab(Material mud)
        {
            const string name = "PF_WallButtress_Simple";
            string path = SampleRoot + "/" + name + ".prefab";
            DeleteExistingAsset(path);
            GameObject root = new GameObject(name);
            CreateCube("Buttress", root.transform, new Vector3(0f, 0.9f, 0.15f), new Vector3(0.70f, 1.8f, 0.75f), mud);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateNichePrefab(Material mud, Material shadow)
        {
            const string name = "PF_WallNiche_Simple";
            string path = SampleRoot + "/" + name + ".prefab";
            DeleteExistingAsset(path);
            GameObject root = new GameObject(name);
            CreateCube("Shadow", root.transform, new Vector3(0f, 0f, 0.015f), new Vector3(0.72f, 0.95f, 0.04f), shadow);
            CreateCube("Top", root.transform, new Vector3(0f, 0.52f, 0f), new Vector3(0.92f, 0.12f, 0.12f), mud);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateEndCapPrefab(Material mud)
        {
            const string name = "PF_WallEnd_Broken";
            string path = SampleRoot + "/" + name + ".prefab";
            DeleteExistingAsset(path);
            GameObject root = new GameObject(name);
            CreateCube("Low", root.transform, new Vector3(0f, 0.68f, 0f), new Vector3(0.58f, 1.36f, 0.48f), mud);
            GameObject upper = CreateCube("Upper", root.transform, new Vector3(0.08f, 1.62f, 0.02f), new Vector3(0.42f, 0.80f, 0.42f), mud);
            upper.transform.localRotation = Quaternion.Euler(0f, 0f, -7f);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateGatePrefab(Material mud, Material shadow)
        {
            const string name = "PF_WallGate_ArchSimple";
            string path = SampleRoot + "/" + name + ".prefab";
            DeleteExistingAsset(path);
            GameObject root = new GameObject(name);
            float width = 2.15f;
            float height = 2.65f;
            CreateCube("Shadow", root.transform, new Vector3(0f, 1.15f, -0.05f), new Vector3(1.72f, 2.30f, 0.05f), shadow);
            CreateCube("Pier_L", root.transform, new Vector3(-width * 0.5f, height * 0.5f, 0f), new Vector3(0.24f, height, 0.62f), mud);
            CreateCube("Pier_R", root.transform, new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(0.24f, height, 0.62f), mud);
            CreateCube("Lintel", root.transform, new Vector3(0f, height - 0.12f, 0f), new Vector3(width + 0.20f, 0.24f, 0.62f), mud);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
            return go;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = SampleRoot + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                material.color = color;
                EditorUtility.SetDirty(material);
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Hidden/InternalErrorShader");

            material = new Material(shader);
            material.name = name;
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void DeleteExistingAsset(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }

        private static void DestroyExisting(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
                Object.DestroyImmediate(existing);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "BuildingGen");
            EnsureFolder("Assets/BuildingGen", "Samples");
            EnsureFolder("Assets/BuildingGen/Samples", "Generated");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
