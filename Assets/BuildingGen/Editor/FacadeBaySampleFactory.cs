using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emadgh.BuildingGen.Editor
{
    internal static class FacadeBaySampleFactory
    {
        private const string RootFolder = "Assets/BuildingGen/Samples";
        private const string SampleFolder = RootFolder + "/FacadeBayV05";

        [MenuItem("Tools/BuildingGen/Create Facade Bay v0.5 Demo")]
        public static void CreateDemo()
        {
            EnsureFolder("Assets/BuildingGen", "Samples");
            EnsureFolder(RootFolder, "FacadeBayV05");

            GameObject window = CreateBayPrefab(
                "BG_Sample_Window",
                FacadeBayKind.Window,
                1.2f,
                1.55f,
                0.9f,
                1,
                true,
                true,
                FacadeSurfaceMask.All,
                5f,
                false);

            GameObject door = CreateBayPrefab(
                "BG_Sample_Door",
                FacadeBayKind.Door,
                1.25f,
                2.35f,
                0f,
                1,
                true,
                false,
                FacadeSurfaceMask.Front | FacadeSurfaceMask.Back | FacadeSurfaceMask.Side,
                0.65f,
                false);

            GameObject niche = CreateBayPrefab(
                "BG_Sample_Niche",
                FacadeBayKind.Niche,
                0.9f,
                1.2f,
                1.0f,
                1,
                true,
                true,
                FacadeSurfaceMask.All,
                1.4f,
                true);

            GameObject iwan = CreateBayPrefab(
                "BG_Sample_Iwan",
                FacadeBayKind.Iwan,
                2.6f,
                5.2f,
                0f,
                2,
                true,
                false,
                FacadeSurfaceMask.Front | FacadeSurfaceMask.Courtyard,
                0.32f,
                false);

            GameObject arcade = CreateBayPrefab(
                "BG_Sample_Arcade",
                FacadeBayKind.Arcade,
                2.0f,
                2.65f,
                0f,
                1,
                true,
                false,
                FacadeSurfaceMask.Courtyard | FacadeSurfaceMask.Front,
                0.45f,
                false);

            FacadeBayCatalog catalog = CreateOrLoadAsset<FacadeBayCatalog>(SampleFolder + "/BG_FacadeBayCatalog.asset");
            catalog.prefabs.Clear();
            catalog.prefabs.Add(window);
            catalog.prefabs.Add(door);
            catalog.prefabs.Add(niche);
            catalog.prefabs.Add(iwan);
            catalog.prefabs.Add(arcade);
            catalog.blankWeight = 0.35f;
            catalog.blankWidth = 1.15f;
            EditorUtility.SetDirty(catalog);

            YazdBuildingStyle style = CreateOrLoadAsset<YazdBuildingStyle>(SampleFolder + "/BG_FacadeBayStyle.asset");
            style.wallMaterial = null;
            style.roofMaterial = null;
            style.doorPrefabs.Clear();
            style.windowPrefabs.Clear();
            style.iwanPrefabs.Clear();
            style.arcadePrefabs.Clear();
            style.badgirPrefabs.Clear();
            style.domePrefabs.Clear();
            style.vaultPrefabs.Clear();
            EditorUtility.SetDirty(style);

            GameObject existing = GameObject.Find("BuildingGen_FacadeBay_v05_Demo");
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            GameObject demo = new GameObject("BuildingGen_FacadeBay_v05_Demo");
            Undo.RegisterCreatedObjectUndo(demo, "Create Facade Bay demo");
            BuildingGenerator generator = demo.AddComponent<BuildingGenerator>();
            generator.style = style;
            generator.seed = 20505;
            generator.footprint = BuildingFootprint.Courtyard;
            generator.width = 14f;
            generator.depth = 11f;
            generator.floors = 2;
            generator.floorHeight = 3.1f;
            generator.wallThickness = 0.38f;
            generator.courtyardWidth = 6f;
            generator.courtyardDepth = 4.8f;
            generator.generateGroundFloorDoor = false;
            generator.generateHashtiAndCorridor = false;
            generator.generateIwan = true;
            generator.generateCourtyardArcade = true;
            generator.generateBadgir = false;
            generator.generateRoofForms = false;
            generator.useFacadeBayPlanner = true;
            generator.facadeBayCatalog = catalog;
            generator.facadeBaySurfaces = FacadeSurfaceMask.Front | FacadeSurfaceMask.Courtyard;
            generator.facadeBaysPerFloor = new Vector2Int(3, 5);
            generator.facadeBayFillChance = 0.92f;
            generator.facadeBayHorizontalJitter = 0.05f;
            generator.maxSpecialBaysPerWall = 2;
            generator.keepPrimaryEntranceDoor = false;

            GenerationCoordinator.Generate(generator);
            Selection.activeGameObject = demo;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject CreateBayPrefab(
            string name,
            FacadeBayKind kind,
            float width,
            float height,
            float sill,
            int floorSpan,
            bool allowGround,
            bool allowUpper,
            FacadeSurfaceMask surfaces,
            float weight,
            bool recessedBack)
        {
            string path = SampleFolder + "/" + name + ".prefab";
            GameObject root = new GameObject(name);
            FacadeBayDefinition definition = root.AddComponent<FacadeBayDefinition>();
            definition.kind = kind;
            definition.selectionWeight = weight;
            definition.nominalWidth = width;
            definition.nominalHeight = height;
            definition.sillHeight = sill;
            definition.edgeClearance = kind == FacadeBayKind.Iwan ? 0.7f : 0.35f;
            definition.neighborClearance = kind == FacadeBayKind.Iwan || kind == FacadeBayKind.Arcade ? 0.4f : 0.25f;
            definition.floorSpan = floorSpan;
            definition.allowGroundFloor = allowGround;
            definition.allowUpperFloors = allowUpper;
            definition.allowedSurfaces = surfaces;
            definition.cutHostSurface = true;

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            definition.visualRoot = visual.transform;
            if (recessedBack)
                definition.visualDepthOffset = -0.08f;

            BuildFrame(visual.transform, kind, width, height);

            GameObject cutter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cutter.name = "Cutter";
            cutter.transform.SetParent(root.transform, false);
            cutter.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            cutter.transform.localScale = new Vector3(width, height, 1.2f);
            MeshRenderer cutterRenderer = cutter.GetComponent<MeshRenderer>();
            if (cutterRenderer != null)
                cutterRenderer.enabled = false;
            Collider cutterCollider = cutter.GetComponent<Collider>();
            if (cutterCollider != null)
                Object.DestroyImmediate(cutterCollider);
            definition.cutterRoot = cutter.transform;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildFrame(Transform parent, FacadeBayKind kind, float width, float height)
        {
            float bar = Mathf.Clamp(width * 0.1f, 0.08f, 0.18f);
            float depth = 0.12f;

            if (kind == FacadeBayKind.Niche)
            {
                CreateBar("Back", parent, new Vector3(0f, height * 0.5f, 0.08f), new Vector3(width * 0.88f, height * 0.88f, 0.08f));
            }

            CreateBar("Left", parent, new Vector3(-width * 0.5f + bar * 0.5f, height * 0.5f, 0f), new Vector3(bar, height, depth));
            CreateBar("Right", parent, new Vector3(width * 0.5f - bar * 0.5f, height * 0.5f, 0f), new Vector3(bar, height, depth));
            CreateBar("Top", parent, new Vector3(0f, height - bar * 0.5f, 0f), new Vector3(width, bar, depth));

            if (kind == FacadeBayKind.Window || kind == FacadeBayKind.Niche)
                CreateBar("Bottom", parent, new Vector3(0f, bar * 0.5f, 0f), new Vector3(width, bar, depth));
        }

        private static void CreateBar(string name, Transform parent, Vector3 localPosition, Vector3 localScale)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPosition;
            bar.transform.localScale = localScale;
            Collider collider = bar.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
        }

        private static T CreateOrLoadAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
