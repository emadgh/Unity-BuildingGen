using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emadgh.BuildingGen.Editor
{
    internal static class WallSplineBuilder
    {
        private const string GeneratedRootName = "__GeneratedWallSpline";

        public static void Generate(WallSpline spline)
        {
            if (spline == null)
                return;

            if (spline.style == null)
            {
                Debug.LogError("BuildingGen WallSpline: assign a WallSplineStyle before generating.", spline);
                return;
            }

            Clear(spline);

            GameObject root = new GameObject(GeneratedRootName);
            Undo.RegisterCreatedObjectUndo(root, "Generate wall spline");
            root.transform.SetParent(spline.transform, false);
            spline.generatedRoot = root.transform;

            Transform wallRoot = CreateGroup("Wall", root.transform);
            Transform topRoot = CreateGroup("TopPrefabs", root.transform);

            List<Vector3> path = GetSampledPath(spline);
            if (path.Count < 2)
                return;

            System.Random random = new System.Random(spline.seed);
            GenerateWallChunks(spline, path, wallRoot, random);
            GenerateTopPrefabs(spline, path, topRoot, random);

            EditorUtility.SetDirty(spline);
        }

        public static void Clear(WallSpline spline)
        {
            if (spline == null)
                return;

            Transform existing = spline.generatedRoot;
            if (existing == null)
                existing = spline.transform.Find(GeneratedRootName);

            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            spline.generatedRoot = null;
            EditorUtility.SetDirty(spline);
        }

        public static GameObject BakeCopy(WallSpline spline)
        {
            if (spline == null || spline.generatedRoot == null)
                return null;

            GameObject baked = Object.Instantiate(spline.generatedRoot.gameObject);
            baked.name = spline.name + "_BakedWall_" + spline.seed;
            baked.transform.SetParent(spline.transform.parent, true);
            Undo.RegisterCreatedObjectUndo(baked, "Bake wall spline");
            return baked;
        }

        public static List<Vector3> GetSampledPath(WallSpline spline)
        {
            List<Vector3> result = new List<Vector3>();
            if (spline == null || spline.controlPoints == null || spline.controlPoints.Count < 2)
                return result;

            if (spline.interpolation == WallSplineInterpolation.Polyline)
            {
                result.AddRange(spline.controlPoints);
                if (spline.closedLoop)
                    result.Add(spline.controlPoints[0]);
                return result;
            }

            int count = spline.controlPoints.Count;
            int segmentCount = spline.closedLoop ? count : count - 1;
            int subdivisions = Mathf.Max(1, spline.smoothSubdivisions);

            for (int segment = 0; segment < segmentCount; segment++)
            {
                Vector3 p0 = GetControlPoint(spline.controlPoints, segment - 1, spline.closedLoop);
                Vector3 p1 = GetControlPoint(spline.controlPoints, segment, spline.closedLoop);
                Vector3 p2 = GetControlPoint(spline.controlPoints, segment + 1, spline.closedLoop);
                Vector3 p3 = GetControlPoint(spline.controlPoints, segment + 2, spline.closedLoop);

                if (segment == 0)
                    result.Add(p1);

                for (int step = 1; step <= subdivisions; step++)
                {
                    float t = step / (float)subdivisions;
                    result.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            return result;
        }

        private static void GenerateWallChunks(
            WallSpline spline,
            List<Vector3> path,
            Transform parent,
            System.Random random)
        {
            WallSplineStyle style = spline.style;
            bool ruined = style.construction == WallSplineConstruction.RuinedMud;
            int chunkIndex = 0;

            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 a = path[i];
                Vector3 b = path[i + 1];
                Vector3 planarDelta = new Vector3(b.x - a.x, 0f, b.z - a.z);
                float planarLength = planarDelta.magnitude;
                if (planarLength < 0.001f)
                    continue;

                int chunks = Mathf.Max(1, Mathf.CeilToInt(planarLength / style.chunkLength));
                for (int c = 0; c < chunks; c++)
                {
                    float t0 = c / (float)chunks;
                    float t1 = (c + 1) / (float)chunks;
                    Vector3 ca = Vector3.Lerp(a, b, t0);
                    Vector3 cb = Vector3.Lerp(a, b, t1);
                    Vector3 chunkDelta = new Vector3(cb.x - ca.x, 0f, cb.z - ca.z);
                    float length = chunkDelta.magnitude;
                    if (length < 0.001f)
                        continue;

                    if (ruined && Next01(random) < style.missingChunkChance)
                    {
                        chunkIndex++;
                        continue;
                    }

                    float height = style.wallHeight;
                    float thickness = style.wallThickness;
                    float baseJitter = 0f;
                    float yawJitter = 0f;

                    if (ruined)
                    {
                        height += Range(random, -style.heightVariation, style.heightVariation * 0.28f);
                        height = Mathf.Max(0.35f, height);
                        thickness += Range(random, -style.thicknessVariation, style.thicknessVariation);
                        thickness = Mathf.Max(0.08f, thickness);
                        baseJitter = Range(random, -style.baseVerticalJitter, style.baseVerticalJitter);
                        yawJitter = Range(random, -style.yawJitter, style.yawJitter);
                    }

                    Vector3 midpoint = (ca + cb) * 0.5f;
                    midpoint.y += height * 0.5f + baseJitter;
                    Quaternion rotation = Quaternion.LookRotation(chunkDelta.normalized, Vector3.up) * Quaternion.Euler(0f, yawJitter, 0f);

                    GameObject piece = CreateCube(
                        "WallChunk_" + chunkIndex.ToString("000"),
                        parent,
                        midpoint,
                        rotation,
                        new Vector3(thickness, height, length + 0.015f),
                        style.wallMaterial,
                        spline.generateCollider);

                    if (ruined)
                    {
                        float lateral = Range(random, -style.thicknessVariation * 0.35f, style.thicknessVariation * 0.35f);
                        piece.transform.localPosition += piece.transform.right * lateral;
                    }

                    chunkIndex++;
                }
            }
        }

        private static void GenerateTopPrefabs(
            WallSpline spline,
            List<Vector3> path,
            Transform parent,
            System.Random random)
        {
            WallSplineStyle style = spline.style;
            if (!style.generateTopPrefabs || style.topPrefabs == null || style.topPrefabs.Count == 0)
                return;

            float spacing = Mathf.Max(0.2f, style.topSpacing);
            float totalLength = CalculateLength(path);
            if (totalLength < spacing * 0.35f)
                return;

            float distance = spacing * 0.5f;
            int index = 0;
            while (distance < totalLength)
            {
                if (Next01(random) <= style.topSpawnChance)
                {
                    Vector3 position;
                    Vector3 tangent;
                    if (EvaluatePathAtDistance(path, distance, out position, out tangent))
                    {
                        GameObject prefab = PickTopPrefab(style.topPrefabs, random);
                        if (prefab != null)
                        {
                            GameObject instance = InstantiatePrefab(prefab, parent);
                            instance.name = prefab.name + "_" + index.ToString("000");

                            Vector3 right = Vector3.Cross(Vector3.up, tangent.normalized);
                            position.y += style.wallHeight + style.topVerticalOffset;
                            position += right * Range(random, -style.topLateralJitter, style.topLateralJitter);
                            instance.transform.localPosition = position;

                            float yaw = Range(random, -style.topYawJitter, style.topYawJitter);
                            instance.transform.localRotation = Quaternion.LookRotation(tangent.normalized, Vector3.up) * Quaternion.Euler(0f, yaw, 0f);

                            float scale = Range(random, style.topScaleRange.x, style.topScaleRange.y);
                            instance.transform.localScale = instance.transform.localScale * scale;
                        }
                    }
                }

                distance += spacing;
                index++;
            }
        }

        private static GameObject PickTopPrefab(List<WallTopPrefabOption> options, System.Random random)
        {
            float total = 0f;
            for (int i = 0; i < options.Count; i++)
            {
                WallTopPrefabOption option = options[i];
                if (option != null && option.prefab != null)
                    total += Mathf.Max(0.001f, option.weight);
            }

            if (total <= 0f)
                return null;

            float pick = Range(random, 0f, total);
            float cursor = 0f;
            for (int i = 0; i < options.Count; i++)
            {
                WallTopPrefabOption option = options[i];
                if (option == null || option.prefab == null)
                    continue;

                cursor += Mathf.Max(0.001f, option.weight);
                if (pick <= cursor)
                    return option.prefab;
            }

            return null;
        }

        private static float CalculateLength(List<Vector3> path)
        {
            float total = 0f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 delta = path[i + 1] - path[i];
                delta.y = 0f;
                total += delta.magnitude;
            }
            return total;
        }

        private static bool EvaluatePathAtDistance(List<Vector3> path, float targetDistance, out Vector3 position, out Vector3 tangent)
        {
            float cursor = 0f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 a = path[i];
                Vector3 b = path[i + 1];
                Vector3 planar = new Vector3(b.x - a.x, 0f, b.z - a.z);
                float length = planar.magnitude;
                if (length < 0.001f)
                    continue;

                if (cursor + length >= targetDistance)
                {
                    float t = Mathf.Clamp01((targetDistance - cursor) / length);
                    position = Vector3.Lerp(a, b, t);
                    tangent = planar.normalized;
                    return true;
                }

                cursor += length;
            }

            position = Vector3.zero;
            tangent = Vector3.forward;
            return false;
        }

        private static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(prefab))
                return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            return Object.Instantiate(prefab, parent);
        }

        private static Vector3 GetControlPoint(List<Vector3> points, int index, bool closed)
        {
            int count = points.Count;
            if (closed)
            {
                int wrapped = ((index % count) + count) % count;
                return points[wrapped];
            }

            return points[Mathf.Clamp(index, 0, count - 1)];
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) +
                           (-p0 + p2) * t +
                           (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject CreateCube(
            string name,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material,
            bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;

            Collider collider = go.GetComponent<Collider>();
            if (!keepCollider && collider != null)
                Object.DestroyImmediate(collider);

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;

            return go;
        }

        private static float Next01(System.Random random)
        {
            return (float)random.NextDouble();
        }

        private static float Range(System.Random random, float min, float max)
        {
            return Mathf.Lerp(min, max, Next01(random));
        }
    }
}
