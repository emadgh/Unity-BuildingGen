using UnityEditor;
using UnityEngine;

namespace Emadgh.BuildingGen.Editor
{
    internal static class WallSplineSampleFactory
    {
        private const string SampleRoot = "Assets/BuildingGen/Samples/Generated";

        [MenuItem("Tools/BuildingGen/Create Wall Spline Demo")]
        public static void CreateWallSplineDemo()
        {
            EnsureFolders();

            Material mud = GetOrCreateMaterial("MAT_WallSpline_Mud", new Color(0.64f, 0.47f, 0.30f));
            Material paleMud = GetOrCreateMaterial("MAT_WallSpline_PaleMud", new Color(0.73f, 0.58f, 0.39f));
            Material brick = GetOrCreateMaterial("MAT_WallSpline_Brick", new Color(0.47f, 0.25f, 0.13f));

            GameObject brickCap = CreateTopCapPrefab("PF_WallTop_BrickCap", brick, false);
            GameObject brokenCap = CreateTopCapPrefab("PF_WallTop_BrokenCap", brick, true);

            WallSplineStyle simple = GetOrCreateStyle("WallStyle_SimpleMud");
            simple.construction = WallSplineConstruction.SimpleMud;
            simple.wallMaterial = mud;
            simple.wallHeight = 2.35f;
            simple.wallThickness = 0.42f;
            simple.chunkLength = 1.35f;
            simple.generateTopPrefabs = false;
            EditorUtility.SetDirty(simple);

            WallSplineStyle ruined = GetOrCreateStyle("WallStyle_RuinedMud");
            ruined.construction = WallSplineConstruction.RuinedMud;
            ruined.wallMaterial = paleMud;
            ruined.wallHeight = 2.15f;
            ruined.wallThickness = 0.48f;
            ruined.chunkLength = 0.85f;
            ruined.heightVariation = 0.90f;
            ruined.thicknessVariation = 0.10f;
            ruined.missingChunkChance = 0.12f;
            ruined.baseVerticalJitter = 0.055f;
            ruined.yawJitter = 4.5f;
            ruined.generateTopPrefabs = false;
            EditorUtility.SetDirty(ruined);

            WallSplineStyle decorated = GetOrCreateStyle("WallStyle_DecoratedMud");
            decorated.construction = WallSplineConstruction.DecoratedMud;
            decorated.wallMaterial = mud;
            decorated.wallHeight = 2.55f;
            decorated.wallThickness = 0.45f;
            decorated.chunkLength = 1.15f;
            decorated.generateTopPrefabs = true;
            decorated.topSpacing = 1.15f;
            decorated.topSpawnChance = 0.92f;
            decorated.topVerticalOffset = 0.03f;
            decorated.topLateralJitter = 0.035f;
            decorated.topYawJitter = 5f;
            decorated.topScaleRange = new Vector2(0.90f, 1.08f);
            decorated.topPrefabs.Clear();
            decorated.topPrefabs.Add(new WallTopPrefabOption { prefab = brickCap, weight = 3f });
            decorated.topPrefabs.Add(new WallTopPrefabOption { prefab = brokenCap, weight = 1f });
            EditorUtility.SetDirty(decorated);

            DestroyExisting("WallSpline_SimpleDemo");
            DestroyExisting("WallSpline_RuinedDemo");
            DestroyExisting("WallSpline_DecoratedDemo");

            WallSpline simpleSpline = CreateSplineObject(
                "WallSpline_SimpleDemo",
                simple,
                new Vector3(0f, 0f, 0f),
                WallSplineInterpolation.Polyline,
                2101,
                new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(4.5f, 0f, 0f),
                    new Vector3(7.2f, 0f, 2.0f),
                    new Vector3(10.0f, 0f, 1.4f)
                });

            WallSpline ruinedSpline = CreateSplineObject(
                "WallSpline_RuinedDemo",
                ruined,
                new Vector3(0f, 0f, -4.0f),
                WallSplineInterpolation.Polyline,
                2102,
                new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(3.1f, 0f, 0.6f),
                    new Vector3(6.0f, 0f, -0.2f),
                    new Vector3(9.6f, 0f, 1.0f)
                });

            WallSpline decoratedSpline = CreateSplineObject(
                "WallSpline_DecoratedDemo",
                decorated,
                new Vector3(0f, 0f, -8.0f),
                WallSplineInterpolation.SmoothCatmullRom,
                2103,
                new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(3.2f, 0f, 0.4f),
                    new Vector3(6.0f, 0f, 1.6f),
                    new Vector3(9.4f, 0f, 0.3f),
                    new Vector3(12.2f, 0f, 1.0f)
                });
            decoratedSpline.smoothSubdivisions = 6;

            WallSplineBuilder.Generate(simpleSpline);
            WallSplineBuilder.Generate(ruinedSpline);
            WallSplineBuilder.Generate(decoratedSpline);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = decoratedSpline.gameObject;

            Debug.Log("BuildingGen: Wall Spline demo created. Drag points in Scene view or Shift+Click to append points.", decoratedSpline);
        }

        private static WallSpline CreateSplineObject(
            string name,
            WallSplineStyle style,
            Vector3 position,
            WallSplineInterpolation interpolation,
            int seed,
            Vector3[] points)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create wall spline demo");
            go.transform.position = position;

            WallSpline spline = go.AddComponent<WallSpline>();
            spline.style = style;
            spline.interpolation = interpolation;
            spline.seed = seed;
            spline.controlPoints.Clear();
            spline.controlPoints.AddRange(points);
            spline.generateCollider = true;
            return spline;
        }

        private static WallSplineStyle GetOrCreateStyle(string name)
        {
            string path = SampleRoot + "/" + name + ".asset";
            WallSplineStyle style = AssetDatabase.LoadAssetAtPath<WallSplineStyle>(path);
            if (style != null)
                return style;

            style = ScriptableObject.CreateInstance<WallSplineStyle>();
            style.name = name;
            AssetDatabase.CreateAsset(style, path);
            return style;
        }

        private static GameObject CreateTopCapPrefab(string name, Material material, bool broken)
        {
            string path = SampleRoot + "/" + name + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);

            GameObject root = new GameObject(name);
            if (!broken)
            {
                CreateCube("Cap", root.transform, new Vector3(0f, 0.10f, 0f), new Vector3(0.75f, 0.20f, 0.55f), material, 0f);
            }
            else
            {
                CreateCube("Broken_A", root.transform, new Vector3(-0.18f, 0.09f, 0f), new Vector3(0.42f, 0.18f, 0.50f), material, -5f);
                CreateCube("Broken_B", root.transform, new Vector3(0.20f, 0.07f, 0.02f), new Vector3(0.32f, 0.14f, 0.44f), material, 8f);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material, float yaw)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
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
