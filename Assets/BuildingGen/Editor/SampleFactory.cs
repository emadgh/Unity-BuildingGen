using UnityEditor;
using UnityEngine;

namespace Emadgh.BuildingGen.Editor
{
    internal static class SampleFactory
    {
        private const string SampleRoot = "Assets/BuildingGen/Samples/Generated";

        [MenuItem("Tools/BuildingGen/Create Simple Yazd Demo")]
        public static void CreateSimpleYazdDemo()
        {
            EnsureFolders();

            Material mud = GetOrCreateMaterial("MAT_YazdMud", new Color(0.66f, 0.49f, 0.31f));
            Material wood = GetOrCreateMaterial("MAT_DarkWood", new Color(0.18f, 0.10f, 0.055f));
            Material dark = GetOrCreateMaterial("MAT_DeepShadow", new Color(0.07f, 0.055f, 0.045f));

            GameObject archWindow = CreateOpeningPrefab(
                "PF_Window_Arch_Simple",
                OpeningKind.Window,
                1.20f,
                1.80f,
                0.90f,
                mud,
                wood,
                false);

            GameObject archDoor = CreateOpeningPrefab(
                "PF_Door_Arch_Simple",
                OpeningKind.Door,
                1.15f,
                2.35f,
                0f,
                mud,
                wood,
                true);

            GameObject iwan = CreateIwanPrefab(mud, dark);
            GameObject badgir = CreateBadgirPrefab(mud, dark);

            string stylePath = SampleRoot + "/YazdStyle_Simple.asset";
            YazdBuildingStyle style = AssetDatabase.LoadAssetAtPath<YazdBuildingStyle>(stylePath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<YazdBuildingStyle>();
                AssetDatabase.CreateAsset(style, stylePath);
            }

            style.wallMaterial = mud;
            style.roofMaterial = mud;
            style.doorPrefabs.Clear();
            style.windowPrefabs.Clear();
            style.iwanPrefabs.Clear();
            style.badgirPrefabs.Clear();
            style.doorPrefabs.Add(archDoor);
            style.windowPrefabs.Add(archWindow);
            style.iwanPrefabs.Add(iwan);
            style.badgirPrefabs.Add(badgir);
            style.frontWindowChance = 1f;
            style.backWindowChance = 0.25f;
            style.sideWindowChance = 0.25f;
            style.courtyardWindowChance = 0.95f;
            style.centeredDoorChance = 0.78f;
            style.iwanChance = 0.85f;
            style.badgirChance = 0.9f;
            style.generateFlatRoof = true;
            style.generateParapet = true;
            style.badgirRoofInset = 0.9f;
            EditorUtility.SetDirty(style);

            GameObject existing = GameObject.Find("YazdBuilding_Demo");
            if (existing != null)
                Object.DestroyImmediate(existing);

            GameObject go = new GameObject("YazdBuilding_Demo");
            Undo.RegisterCreatedObjectUndo(go, "Create BuildingGen demo");
            BuildingGenerator generator = go.AddComponent<BuildingGenerator>();
            generator.style = style;
            generator.footprint = BuildingFootprint.Courtyard;
            generator.width = 13f;
            generator.depth = 11f;
            generator.courtyardWidth = 5.6f;
            generator.courtyardDepth = 4.7f;
            generator.floors = 2;
            generator.floorHeight = 3.05f;
            generator.wallThickness = 0.38f;
            generator.windowsPerFloor = new Vector2Int(1, 3);
            generator.generateSideOpenings = false;
            generator.generateCourtyardOpenings = true;
            generator.recessedEntrance = true;
            generator.entranceRecess = 0.42f;
            generator.generateIwan = true;
            generator.generateBadgir = true;
            generator.badgirCount = 1;
            generator.seed = 1403;

            BuildingBuilder.Generate(generator);
            Selection.activeGameObject = go;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("BuildingGen: Yazd courtyard demo created. Try the footprint preset buttons and change Seed on YazdBuilding_Demo.", go);
        }

        private static GameObject CreateOpeningPrefab(
            string name,
            OpeningKind kind,
            float width,
            float height,
            float sill,
            Material mud,
            Material wood,
            bool isDoor)
        {
            string prefabPath = SampleRoot + "/" + name + ".prefab";
            GameObject previous = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (previous != null)
                AssetDatabase.DeleteAsset(prefabPath);

            GameObject root = new GameObject(name);
            OpeningDefinition definition = root.AddComponent<OpeningDefinition>();
            definition.kind = kind;
            definition.nominalWidth = width;
            definition.nominalHeight = height;
            definition.sillHeight = sill;
            definition.edgeClearance = 0.45f;
            definition.neighborClearance = 0.40f;
            definition.allowGroundFloor = true;
            definition.allowUpperFloors = !isDoor;

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            definition.visualRoot = visual.transform;

            float frame = 0.10f;
            float depth = 0.12f;
            CreateVisualCube("Frame_L", visual.transform,
                new Vector3(-width * 0.5f - frame * 0.5f, height * 0.5f, 0.01f),
                new Vector3(frame, height, depth), mud);
            CreateVisualCube("Frame_R", visual.transform,
                new Vector3(width * 0.5f + frame * 0.5f, height * 0.5f, 0.01f),
                new Vector3(frame, height, depth), mud);

            CreateArchTrim(visual.transform, width, height, frame, depth, mud);

            if (isDoor)
            {
                CreateVisualCube("DoorPanel", visual.transform,
                    new Vector3(0f, height * 0.46f, -0.03f),
                    new Vector3(width * 0.90f, height * 0.88f, 0.08f), wood);
            }
            else
            {
                CreateVisualCube("WindowBar_V", visual.transform,
                    new Vector3(0f, height * 0.43f, -0.02f),
                    new Vector3(0.055f, height * 0.70f, 0.06f), wood);
                CreateVisualCube("WindowBar_H", visual.transform,
                    new Vector3(0f, height * 0.44f, -0.02f),
                    new Vector3(width * 0.82f, 0.055f, 0.06f), wood);
            }

            GameObject cutter = new GameObject("Cutter");
            cutter.transform.SetParent(root.transform, false);
            MeshFilter cutterFilter = cutter.AddComponent<MeshFilter>();
            MeshRenderer cutterRenderer = cutter.AddComponent<MeshRenderer>();
            cutterRenderer.sharedMaterial = mud;
            cutterRenderer.enabled = false;

            string meshPath = SampleRoot + "/" + name + "_Cutter.asset";
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existingMesh != null)
                AssetDatabase.DeleteAsset(meshPath);

            Mesh cutterMesh = EditorMeshFactory.CreateArchPrism(width, height, 1.2f, 12);
            AssetDatabase.CreateAsset(cutterMesh, meshPath);
            cutterFilter.sharedMesh = cutterMesh;
            definition.cutterRoot = cutter.transform;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateIwanPrefab(Material mud, Material dark)
        {
            const string name = "PF_Iwan_Simple";
            string path = SampleRoot + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                AssetDatabase.DeleteAsset(path);

            GameObject root = new GameObject(name);
            YazdModuleDefinition definition = root.AddComponent<YazdModuleDefinition>();
            definition.kind = YazdModuleKind.Iwan;
            definition.nominalWidth = 2.8f;
            definition.nominalDepth = 0.75f;
            definition.nominalHeight = 3.4f;
            definition.selectionWeight = 1f;

            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            float width = 2.8f;
            float height = 3.4f;
            float frame = 0.22f;
            CreateVisualCube("IwanShadow", visual, new Vector3(0f, height * 0.46f, -0.18f), new Vector3(width * 0.82f, height * 0.84f, 0.08f), dark);
            CreateVisualCube("Pier_L", visual, new Vector3(-width * 0.5f, height * 0.5f, 0f), new Vector3(frame, height, 0.72f), mud);
            CreateVisualCube("Pier_R", visual, new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(frame, height, 0.72f), mud);
            CreateArchTrim(visual, width, height, frame, 0.72f, mud);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateBadgirPrefab(Material mud, Material dark)
        {
            const string name = "PF_Badgir_Simple";
            string path = SampleRoot + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                AssetDatabase.DeleteAsset(path);

            GameObject root = new GameObject(name);
            YazdModuleDefinition definition = root.AddComponent<YazdModuleDefinition>();
            definition.kind = YazdModuleKind.Badgir;
            definition.nominalWidth = 1.6f;
            definition.nominalDepth = 1.6f;
            definition.nominalHeight = 3.8f;
            definition.selectionWeight = 1f;

            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            CreateVisualCube("Tower", visual, new Vector3(0f, 1.65f, 0f), new Vector3(1.55f, 3.3f, 1.55f), mud);
            CreateVisualCube("TopCap", visual, new Vector3(0f, 3.42f, 0f), new Vector3(1.78f, 0.22f, 1.78f), mud);

            float slitY = 2.65f;
            CreateVisualCube("Slit_Front", visual, new Vector3(0f, slitY, 0.786f), new Vector3(0.62f, 1.05f, 0.035f), dark);
            CreateVisualCube("Slit_Back", visual, new Vector3(0f, slitY, -0.786f), new Vector3(0.62f, 1.05f, 0.035f), dark);
            CreateVisualCube("Slit_Left", visual, new Vector3(-0.786f, slitY, 0f), new Vector3(0.035f, 1.05f, 0.62f), dark);
            CreateVisualCube("Slit_Right", visual, new Vector3(0.786f, slitY, 0f), new Vector3(0.035f, 1.05f, 0.62f), dark);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateArchTrim(
            Transform parent,
            float width,
            float height,
            float thickness,
            float depth,
            Material material)
        {
            int segments = 10;
            float radius = width * 0.5f + thickness * 0.45f;
            float springY = height - width * 0.5f;

            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.Lerp(0f, Mathf.PI, i / (float)segments);
                float a1 = Mathf.Lerp(0f, Mathf.PI, (i + 1) / (float)segments);
                float am = (a0 + a1) * 0.5f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, springY + Mathf.Sin(a0) * radius, 0f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, springY + Mathf.Sin(a1) * radius, 0f);
                Vector3 pm = (p0 + p1) * 0.5f;
                float length = Vector3.Distance(p0, p1) + 0.015f;

                GameObject piece = CreateVisualCube(
                    "ArchTrim_" + i,
                    parent,
                    pm,
                    new Vector3(length, thickness, depth),
                    material);
                piece.transform.localRotation = Quaternion.Euler(0f, 0f, am * Mathf.Rad2Deg + 90f);
            }
        }

        private static GameObject CreateVisualCube(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
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
