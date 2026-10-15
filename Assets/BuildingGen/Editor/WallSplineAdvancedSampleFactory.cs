using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emadgh.BuildingGen.Editor
{
    internal static class WallSplineAdvancedSampleFactory
    {
        private const string RootFolder = "Assets/BuildingGen/Samples";
        private const string SampleFolder = RootFolder + "/WallSplineV06";

        [MenuItem("Tools/BuildingGen/Create Wall Spline v0.6 Advanced Demo")]
        public static void CreateDemo()
        {
            EnsureFolder("Assets/BuildingGen", "Samples");
            EnsureFolder(RootFolder, "WallSplineV06");

            GameObject gateFrame = CreateGateFramePrefab();
            WallSplineStyle style = CreateOrLoadAsset<WallSplineStyle>(SampleFolder + "/BG_WallSpline_Advanced.asset");
            style.construction = WallSplineConstruction.RuinedMud;
            style.wallHeight = 2.7f;
            style.wallThickness = 0.44f;
            style.chunkLength = 0.75f;
            style.heightVariation = 0.22f;
            style.thicknessVariation = 0.04f;
            style.missingChunkChance = 0.03f;
            style.baseVerticalJitter = 0.015f;
            style.yawJitter = 1.2f;
            style.generateBrokenTopProfile = true;
            style.brokenTopChance = 0.45f;
            style.brokenTopDropRange = new Vector2(0.08f, 0.36f);
            style.generateButtresses = true;
            style.buttressSpacing = 3.0f;
            style.buttressChance = 1f;
            style.buttressWidth = 0.34f;
            style.buttressExtraDepth = 0.24f;
            style.buttressHeightScale = 0.82f;
            style.buttressSide = WallButtressSide.Right;
            style.generateEndCaps = true;
            style.endCapWidth = 0.5f;
            style.endCapExtraDepth = 0.16f;
            style.generateTopPrefabs = false;
            EditorUtility.SetDirty(style);

            GameObject oldDemo = GameObject.Find("WallSpline_Advanced_v06_Demo");
            if (oldDemo != null)
                Undo.DestroyObjectImmediate(oldDemo);

            GameObject oldGround = GameObject.Find("WallSpline_Advanced_v06_Ground");
            if (oldGround != null)
                Undo.DestroyObjectImmediate(oldGround);

            GameObject groundRoot = new GameObject("WallSpline_Advanced_v06_Ground");
            Undo.RegisterCreatedObjectUndo(groundRoot, "Create WallSpline v0.6 ground");
            CreateGroundStep(groundRoot.transform, "Ground_A", new Vector3(1.8f, -0.18f, 0.2f), new Vector3(4.2f, 0.36f, 5f));
            CreateGroundStep(groundRoot.transform, "Ground_B", new Vector3(5.9f, 0.02f, 0.65f), new Vector3(4.2f, 0.4f, 5f));
            CreateGroundStep(groundRoot.transform, "Ground_C", new Vector3(10.1f, 0.25f, 1.7f), new Vector3(5.2f, 0.5f, 5f));

            GameObject demo = new GameObject("WallSpline_Advanced_v06_Demo");
            Undo.RegisterCreatedObjectUndo(demo, "Create WallSpline v0.6 demo");
            WallSpline spline = demo.AddComponent<WallSpline>();
            spline.style = style;
            spline.seed = 26006;
            spline.interpolation = WallSplineInterpolation.SmoothCatmullRom;
            spline.smoothSubdivisions = 5;
            spline.controlPoints.Clear();
            spline.controlPoints.Add(new Vector3(0f, 0f, 0f));
            spline.controlPoints.Add(new Vector3(3.8f, 0f, 0.15f));
            spline.controlPoints.Add(new Vector3(7.6f, 0f, 1.1f));
            spline.controlPoints.Add(new Vector3(12.4f, 0f, 2.0f));
            spline.conformToTerrain = true;
            spline.terrainLayers = ~0;
            spline.terrainRaycastHeight = 8f;
            spline.terrainRaycastDepth = 20f;
            spline.terrainVerticalOffset = 0f;
            spline.openings.Clear();
            spline.openings.Add(new WallSplineOpening
            {
                enabled = true,
                distance = 6.2f,
                width = 2.1f,
                clearHeight = 2.15f,
                framePrefab = gateFrame,
                verticalOffset = 0f,
                uniformScale = 1f,
                generateEdgePillars = true,
                pillarWidth = 0.32f,
                pillarExtraDepth = 0.2f
            });

            WallSplineBuilder.Generate(spline);
            Selection.activeGameObject = demo;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject CreateGateFramePrefab()
        {
            string path = SampleFolder + "/BG_Sample_GateFrame.prefab";
            GameObject root = new GameObject("BG_Sample_GateFrame");

            CreateBar("Left", root.transform, new Vector3(-0.96f, 1.1f, 0f), new Vector3(0.18f, 2.2f, 0.18f));
            CreateBar("Right", root.transform, new Vector3(0.96f, 1.1f, 0f), new Vector3(0.18f, 2.2f, 0.18f));
            CreateBar("Top", root.transform, new Vector3(0f, 2.1f, 0f), new Vector3(2.1f, 0.2f, 0.18f));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateGroundStep(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = name;
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = position;
            ground.transform.localScale = scale;
        }

        private static void CreateBar(string name, Transform parent, Vector3 position, Vector3 scale)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = position;
            bar.transform.localScale = scale;
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
