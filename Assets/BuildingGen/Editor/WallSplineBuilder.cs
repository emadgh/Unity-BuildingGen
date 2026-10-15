using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emadgh.BuildingGen.Editor
{
    internal static class WallSplineBuilder
    {
        private const string GeneratedRootName = "__GeneratedWallSpline";

        private enum PlacementFacing
        {
            AlongPath,
            Outward
        }

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
            Transform openingRoot = CreateGroup("Openings", root.transform);
            Transform topRoot = CreateGroup("TopPrefabs", root.transform);
            Transform detailRoot = CreateGroup("WallDetails", root.transform);

            List<Vector3> path = GetSampledPath(spline);
            if (path.Count < 2)
                return;

            System.Random random = new System.Random(spline.seed);
            GenerateWallChunks(spline, path, wallRoot, random);
            GenerateOpeningPrefabs(spline, path, openingRoot);
            GenerateTopPrefabs(spline, path, topRoot, random);
            GenerateWallDetails(spline, path, detailRoot, random);
            GenerateEndCaps(spline, path, detailRoot, random);

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
            }
            else
            {
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
            }

            if (spline.conformToTerrain)
                ConformPathToTerrain(spline, result);

            return result;
        }

        public static float GetPathLength(WallSpline spline)
        {
            return CalculateLength(GetSampledPath(spline));
        }

        public static bool EvaluateSplineAtDistance(WallSpline spline, float distance, out Vector3 localPosition, out Vector3 localTangent)
        {
            return EvaluatePathAtDistance(GetSampledPath(spline), distance, out localPosition, out localTangent);
        }

        private static void ConformPathToTerrain(WallSpline spline, List<Vector3> path)
        {
            float rayLength = spline.terrainRayHeight + spline.terrainRayDistance;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 world = spline.transform.TransformPoint(path[i]);
                Vector3 origin = world + Vector3.up * spline.terrainRayHeight;
                RaycastHit hit;
                if (!Physics.Raycast(origin, Vector3.down, out hit, rayLength, spline.terrainLayerMask, QueryTriggerInteraction.Ignore))
                    continue;

                Vector3 conformedWorld = hit.point + Vector3.up * spline.terrainOffset;
                path[i] = spline.transform.InverseTransformPoint(conformedWorld);
            }
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
            float pathCursor = 0f;

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

                    float intervalStart = pathCursor + planarLength * t0;
                    float intervalEnd = pathCursor + planarLength * t1;
                    if (IsRangeBlockedByOpening(spline, intervalStart, intervalEnd, 0.03f))
                    {
                        chunkIndex++;
                        continue;
                    }

                    if (ruined && Next01(random) < style.missingChunkChance)
                    {
                        chunkIndex++;
                        continue;
                    }

                    float baseHeight = style.wallHeight;
                    float thickness = style.wallThickness;
                    float baseJitter = 0f;
                    float yawJitter = 0f;

                    if (ruined)
                    {
                        baseHeight += Range(random, -style.heightVariation, style.heightVariation * 0.28f);
                        baseHeight = Mathf.Max(0.35f, baseHeight);
                        thickness += Range(random, -style.thicknessVariation, style.thicknessVariation);
                        thickness = Mathf.Max(0.08f, thickness);
                        baseJitter = Range(random, -style.baseVerticalJitter, style.baseVerticalJitter);
                        yawJitter = Range(random, -style.yawJitter, style.yawJitter);
                    }

                    float lowGround = Mathf.Min(ca.y, cb.y);
                    float groundDelta = Mathf.Abs(cb.y - ca.y);
                    float effectiveHeight = baseHeight + groundDelta;
                    Vector3 midpoint = (ca + cb) * 0.5f;
                    midpoint.y = lowGround + effectiveHeight * 0.5f + baseJitter;
                    Quaternion rotation = Quaternion.LookRotation(chunkDelta.normalized, Vector3.up) * Quaternion.Euler(0f, yawJitter, 0f);

                    GameObject piece = CreateCube(
                        "WallChunk_" + chunkIndex.ToString("000"),
                        parent,
                        midpoint,
                        rotation,
                        new Vector3(thickness, effectiveHeight, length + 0.02f),
                        style.wallMaterial,
                        spline.generateCollider);

                    if (ruined)
                    {
                        float lateral = Range(random, -style.thicknessVariation * 0.35f, style.thicknessVariation * 0.35f);
                        piece.transform.localPosition += (piece.transform.localRotation * Vector3.right) * lateral;
                    }

                    chunkIndex++;
                }

                pathCursor += planarLength;
            }
        }

        private static void GenerateOpeningPrefabs(WallSpline spline, List<Vector3> path, Transform parent)
        {
            if (spline.openings == null || spline.openings.Count == 0)
                return;

            float totalLength = CalculateLength(path);
            for (int i = 0; i < spline.openings.Count; i++)
            {
                WallSplineOpening opening = spline.openings[i];
                if (opening == null || !opening.enabled || opening.prefab == null)
                    continue;

                float distance = Mathf.Clamp(opening.distance, 0f, totalLength);
                Vector3 position;
                Vector3 tangent;
                if (!EvaluatePathAtDistance(path, distance, out position, out tangent))
                    continue;

                Vector3 outward = GetDecorationSide(spline, tangent);
                GameObject instance = InstantiatePrefab(opening.prefab, parent);
                instance.name = opening.prefab.name + "_Gate_" + i.ToString("00");
                position.y += opening.verticalOffset;
                instance.transform.localPosition = position;
                instance.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(opening.localEulerOffset);
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
                if (!IsDistanceBlockedByOpening(spline, distance, spacing * 0.35f) && Next01(random) <= style.topSpawnChance)
                {
                    Vector3 position;
                    Vector3 tangent;
                    if (EvaluatePathAtDistance(path, distance, out position, out tangent))
                    {
                        GameObject prefab = PickPrefab(style.topPrefabs, random);
                        if (prefab != null)
                        {
                            GameObject instance = InstantiatePrefab(prefab, parent);
                            instance.name = prefab.name + "_" + index.ToString("000");

                            Vector3 right = GetDecorationSide(spline, tangent);
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

        private static void GenerateWallDetails(WallSpline spline, List<Vector3> path, Transform parent, System.Random random)
        {
            WallSplineStyle style = spline.style;

            if (style.generatePillars)
            {
                GenerateRepeatedPrefabs(
                    spline, path, parent, random, style.pillarPrefabs,
                    style.pillarSpacing, style.pillarSpawnChance,
                    0f, style.pillarVerticalOffset, style.pillarScaleRange,
                    PlacementFacing.AlongPath, "Pillar");
            }

            if (style.generateButtresses)
            {
                float lateral = style.wallThickness * 0.5f + style.buttressOutwardOffset;
                GenerateRepeatedPrefabs(
                    spline, path, parent, random, style.buttressPrefabs,
                    style.buttressSpacing, style.buttressSpawnChance,
                    lateral, style.buttressVerticalOffset, style.buttressScaleRange,
                    PlacementFacing.Outward, "Buttress");
            }

            if (style.generateNiches)
            {
                float lateral = style.wallThickness * 0.5f + style.nicheSurfaceOffset;
                GenerateRepeatedPrefabs(
                    spline, path, parent, random, style.nichePrefabs,
                    style.nicheSpacing, style.nicheSpawnChance,
                    lateral, style.nicheHeight, style.nicheScaleRange,
                    PlacementFacing.Outward, "Niche");
            }
        }

        private static void GenerateRepeatedPrefabs(
            WallSpline spline,
            List<Vector3> path,
            Transform parent,
            System.Random random,
            List<WallTopPrefabOption> pool,
            float spacing,
            float spawnChance,
            float lateralOffset,
            float verticalOffset,
            Vector2 scaleRange,
            PlacementFacing facing,
            string namePrefix)
        {
            if (pool == null || pool.Count == 0)
                return;

            float totalLength = CalculateLength(path);
            spacing = Mathf.Max(0.2f, spacing);
            float distance = spacing * 0.5f;
            int index = 0;

            while (distance < totalLength)
            {
                if (!IsDistanceBlockedByOpening(spline, distance, spacing * 0.32f) && Next01(random) <= spawnChance)
                {
                    Vector3 position;
                    Vector3 tangent;
                    if (EvaluatePathAtDistance(path, distance, out position, out tangent))
                    {
                        GameObject prefab = PickPrefab(pool, random);
                        if (prefab != null)
                        {
                            Vector3 outward = GetDecorationSide(spline, tangent);
                            position += outward * lateralOffset;
                            position.y += verticalOffset;

                            GameObject instance = InstantiatePrefab(prefab, parent);
                            instance.name = namePrefix + "_" + index.ToString("000") + "_" + prefab.name;
                            instance.transform.localPosition = position;
                            instance.transform.localRotation = facing == PlacementFacing.Outward
                                ? Quaternion.LookRotation(outward, Vector3.up)
                                : Quaternion.LookRotation(tangent.normalized, Vector3.up);

                            float scale = Range(random, scaleRange.x, scaleRange.y);
                            instance.transform.localScale *= scale;
                        }
                    }
                }

                distance += spacing;
                index++;
            }
        }

        private static void GenerateEndCaps(WallSpline spline, List<Vector3> path, Transform parent, System.Random random)
        {
            WallSplineStyle style = spline.style;
            if (spline.closedLoop || !style.generateEndCaps || style.endCapPrefabs == null || style.endCapPrefabs.Count == 0 || path.Count < 2)
                return;

            Vector3 startTangent = Flatten(path[1] - path[0]).normalized;
            Vector3 endTangent = Flatten(path[path.Count - 1] - path[path.Count - 2]).normalized;
            PlaceEndCap(path[0], startTangent, "StartCap", spline, parent, random);
            PlaceEndCap(path[path.Count - 1], -endTangent, "EndCap", spline, parent, random);
        }

        private static void PlaceEndCap(
            Vector3 position,
            Vector3 forward,
            string name,
            WallSpline spline,
            Transform parent,
            System.Random random)
        {
            GameObject prefab = PickPrefab(spline.style.endCapPrefabs, random);
            if (prefab == null)
                return;

            GameObject instance = InstantiatePrefab(prefab, parent);
            instance.name = name + "_" + prefab.name;
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.LookRotation(forward, Vector3.up);
            float scale = Range(random, spline.style.endCapScaleRange.x, spline.style.endCapScaleRange.y);
            instance.transform.localScale *= scale;
        }

        private static bool IsRangeBlockedByOpening(WallSpline spline, float start, float end, float padding)
        {
            if (spline.openings == null)
                return false;

            for (int i = 0; i < spline.openings.Count; i++)
            {
                WallSplineOpening opening = spline.openings[i];
                if (opening == null || !opening.enabled)
                    continue;

                float half = opening.width * 0.5f + padding;
                float openStart = opening.distance - half;
                float openEnd = opening.distance + half;
                if (end >= openStart && start <= openEnd)
                    return true;
            }

            return false;
        }

        private static bool IsDistanceBlockedByOpening(WallSpline spline, float distance, float padding)
        {
            return IsRangeBlockedByOpening(spline, distance, distance, padding);
        }

        private static GameObject PickPrefab(List<WallTopPrefabOption> options, System.Random random)
        {
            if (options == null)
                return null;

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
                Vector3 delta = Flatten(path[i + 1] - path[i]);
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
                Vector3 planar = Flatten(b - a);
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

            if (path.Count >= 2 && targetDistance >= cursor)
            {
                position = path[path.Count - 1];
                tangent = Flatten(path[path.Count - 1] - path[path.Count - 2]).normalized;
                return tangent.sqrMagnitude > 0.001f;
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

        private static Vector3 GetDecorationSide(WallSpline spline, Vector3 tangent)
        {
            Vector3 side = Vector3.Cross(Vector3.up, tangent.normalized).normalized;
            if (spline.flipDecorationSide)
                side = -side;
            return side;
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
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
