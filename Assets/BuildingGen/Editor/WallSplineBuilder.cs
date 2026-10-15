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
            Transform openingRoot = CreateGroup("Openings", root.transform);
            Transform structureRoot = CreateGroup("StructuralDetails", root.transform);
            Transform topRoot = CreateGroup("TopPrefabs", root.transform);

            List<Vector3> path = GetSampledPath(spline);
            if (path.Count < 2)
                return;

            if (spline.conformToTerrain)
                ApplyTerrainConform(spline, path);

            System.Random random = new System.Random(spline.seed);
            GenerateWallChunks(spline, path, wallRoot, random);
            GenerateOpeningFeatures(spline, path, openingRoot, structureRoot);
            GenerateButtresses(spline, path, structureRoot, random);
            GenerateEndCaps(spline, path, structureRoot);
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

        private static void ApplyTerrainConform(WallSpline spline, List<Vector3> path)
        {
            Physics.SyncTransforms();
            float rayHeight = Mathf.Max(0.1f, spline.terrainRaycastHeight);
            float rayDepth = Mathf.Max(0.1f, spline.terrainRaycastDepth);
            float maxDistance = rayHeight + rayDepth;

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 world = spline.transform.TransformPoint(path[i]);
                Vector3 origin = world + Vector3.up * rayHeight;
                RaycastHit hit;
                if (!Physics.Raycast(
                        origin,
                        Vector3.down,
                        out hit,
                        maxDistance,
                        spline.terrainLayers,
                        QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                Vector3 adjustedWorld = hit.point + Vector3.up * spline.terrainVerticalOffset;
                path[i] = spline.transform.InverseTransformPoint(adjustedWorld);
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
            float totalLength = CalculateLength(path);
            if (totalLength < 0.001f)
                return;

            List<float> boundaries = BuildDistanceBoundaries(spline, path, totalLength, style.chunkLength);
            int chunkIndex = 0;

            for (int i = 0; i < boundaries.Count - 1; i++)
            {
                float d0 = boundaries[i];
                float d1 = boundaries[i + 1];
                float intervalLength = d1 - d0;
                if (intervalLength < 0.001f)
                    continue;

                Vector3 a;
                Vector3 tangentA;
                Vector3 b;
                Vector3 tangentB;
                if (!EvaluatePathAtDistance(path, d0, out a, out tangentA) ||
                    !EvaluatePathAtDistance(path, d1, out b, out tangentB))
                {
                    continue;
                }

                Vector3 planarDelta = new Vector3(b.x - a.x, 0f, b.z - a.z);
                float length = planarDelta.magnitude;
                if (length < 0.001f)
                    continue;

                float midDistance = (d0 + d1) * 0.5f;
                WallSplineOpening opening = FindOpeningAtDistance(spline, midDistance, 0f);

                if (ruined && opening == null && Next01(random) < style.missingChunkChance)
                {
                    chunkIndex++;
                    continue;
                }

                float fullHeight = style.wallHeight;
                float thickness = style.wallThickness;
                float baseJitter = 0f;
                float yawJitter = 0f;

                if (ruined)
                {
                    fullHeight += Range(random, -style.heightVariation, style.heightVariation * 0.28f);
                    fullHeight = Mathf.Max(0.35f, fullHeight);
                    thickness += Range(random, -style.thicknessVariation, style.thicknessVariation);
                    thickness = Mathf.Max(0.08f, thickness);
                    baseJitter = Range(random, -style.baseVerticalJitter, style.baseVerticalJitter);
                    yawJitter = Range(random, -style.yawJitter, style.yawJitter);
                }

                if (style.generateBrokenTopProfile && Next01(random) <= style.brokenTopChance)
                {
                    float drop = Range(random, style.brokenTopDropRange.x, style.brokenTopDropRange.y);
                    fullHeight = Mathf.Max(0.35f, fullHeight - drop);
                }

                float pieceHeight = fullHeight;
                float verticalBase = 0f;
                if (opening != null)
                {
                    float clearHeight = Mathf.Clamp(opening.clearHeight, 0.3f, fullHeight);
                    pieceHeight = fullHeight - clearHeight;
                    verticalBase = clearHeight;
                    if (pieceHeight < 0.08f)
                    {
                        chunkIndex++;
                        continue;
                    }
                }

                Vector3 midpoint = (a + b) * 0.5f;
                midpoint.y = (a.y + b.y) * 0.5f + verticalBase + pieceHeight * 0.5f + baseJitter;
                Quaternion rotation = Quaternion.LookRotation(planarDelta.normalized, Vector3.up) * Quaternion.Euler(0f, yawJitter, 0f);

                GameObject piece = CreateCube(
                    "WallChunk_" + chunkIndex.ToString("000"),
                    parent,
                    midpoint,
                    rotation,
                    new Vector3(thickness, pieceHeight, length + 0.015f),
                    style.wallMaterial,
                    spline.generateCollider);

                if (ruined)
                {
                    float lateral = Range(random, -style.thicknessVariation * 0.35f, style.thicknessVariation * 0.35f);
                    piece.transform.localPosition += (piece.transform.localRotation * Vector3.right) * lateral;
                }

                chunkIndex++;
            }
        }

        private static List<float> BuildDistanceBoundaries(
            WallSpline spline,
            List<Vector3> path,
            float totalLength,
            float chunkLength)
        {
            List<float> values = new List<float>();
            values.Add(0f);
            values.Add(totalLength);

            float spacing = Mathf.Max(0.25f, chunkLength);
            for (float distance = spacing; distance < totalLength; distance += spacing)
                values.Add(distance);

            float cursor = 0f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 delta = path[i + 1] - path[i];
                delta.y = 0f;
                cursor += delta.magnitude;
                if (cursor > 0f && cursor < totalLength)
                    values.Add(cursor);
            }

            if (spline.openings != null)
            {
                for (int i = 0; i < spline.openings.Count; i++)
                {
                    WallSplineOpening opening = spline.openings[i];
                    if (opening == null || !opening.enabled)
                        continue;

                    float half = Mathf.Max(0.15f, opening.width * 0.5f);
                    float start = Mathf.Clamp(opening.distance - half, 0f, totalLength);
                    float end = Mathf.Clamp(opening.distance + half, 0f, totalLength);
                    if (end - start < 0.001f)
                        continue;

                    values.Add(start);
                    values.Add(end);
                }
            }

            values.Sort();
            List<float> unique = new List<float>();
            for (int i = 0; i < values.Count; i++)
            {
                float value = values[i];
                if (unique.Count == 0 || Mathf.Abs(value - unique[unique.Count - 1]) > 0.001f)
                    unique.Add(value);
            }

            return unique;
        }

        private static void GenerateOpeningFeatures(
            WallSpline spline,
            List<Vector3> path,
            Transform openingParent,
            Transform structureParent)
        {
            if (spline.openings == null || spline.openings.Count == 0)
                return;

            float totalLength = CalculateLength(path);
            for (int i = 0; i < spline.openings.Count; i++)
            {
                WallSplineOpening opening = spline.openings[i];
                if (opening == null || !opening.enabled)
                    continue;

                float centerDistance = Mathf.Clamp(opening.distance, 0f, totalLength);
                Vector3 center;
                Vector3 tangent;
                if (!EvaluatePathAtDistance(path, centerDistance, out center, out tangent))
                    continue;

                if (opening.framePrefab != null)
                {
                    GameObject instance = InstantiatePrefab(opening.framePrefab, openingParent);
                    instance.name = opening.framePrefab.name + "_Opening_" + i.ToString("00");
                    Vector3 outward = Vector3.Cross(Vector3.up, tangent.normalized).normalized;
                    instance.transform.localPosition = center + Vector3.up * opening.verticalOffset;
                    instance.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);
                    instance.transform.localScale *= Mathf.Max(0.01f, opening.uniformScale);
                }

                if (opening.generateEdgePillars)
                {
                    float half = Mathf.Max(0.15f, opening.width * 0.5f);
                    GenerateOpeningPillar(spline, path, Mathf.Clamp(centerDistance - half, 0f, totalLength), structureParent, i, false, opening);
                    GenerateOpeningPillar(spline, path, Mathf.Clamp(centerDistance + half, 0f, totalLength), structureParent, i, true, opening);
                }
            }
        }

        private static void GenerateOpeningPillar(
            WallSpline spline,
            List<Vector3> path,
            float distance,
            Transform parent,
            int openingIndex,
            bool endSide,
            WallSplineOpening opening)
        {
            Vector3 position;
            Vector3 tangent;
            if (!EvaluatePathAtDistance(path, distance, out position, out tangent))
                return;

            Vector3 right = Vector3.Cross(Vector3.up, tangent.normalized).normalized;
            float depth = spline.style.wallThickness + Mathf.Max(0f, opening.pillarExtraDepth);
            position += right * (Mathf.Max(0f, opening.pillarExtraDepth) * 0.5f);
            position.y += spline.style.wallHeight * 0.5f;

            CreateCube(
                "OpeningPillar_" + openingIndex.ToString("00") + (endSide ? "_B" : "_A"),
                parent,
                position,
                Quaternion.LookRotation(tangent.normalized, Vector3.up),
                new Vector3(depth, spline.style.wallHeight, Mathf.Max(0.08f, opening.pillarWidth)),
                spline.style.wallMaterial,
                spline.generateCollider);
        }

        private static void GenerateButtresses(
            WallSpline spline,
            List<Vector3> path,
            Transform parent,
            System.Random random)
        {
            WallSplineStyle style = spline.style;
            if (!style.generateButtresses)
                return;

            float totalLength = CalculateLength(path);
            float spacing = Mathf.Max(0.4f, style.buttressSpacing);
            float distance = spacing * 0.5f;
            int index = 0;

            while (distance < totalLength)
            {
                if (FindOpeningAtDistance(spline, distance, style.buttressWidth * 0.75f) == null &&
                    Next01(random) <= style.buttressChance)
                {
                    Vector3 position;
                    Vector3 tangent;
                    if (EvaluatePathAtDistance(path, distance, out position, out tangent))
                    {
                        if (style.buttressSide == WallButtressSide.Right || style.buttressSide == WallButtressSide.Both)
                            CreateButtress(spline, position, tangent, parent, index, 1f);
                        if (style.buttressSide == WallButtressSide.Left || style.buttressSide == WallButtressSide.Both)
                            CreateButtress(spline, position, tangent, parent, index, -1f);
                    }
                }

                distance += spacing;
                index++;
            }
        }

        private static void CreateButtress(
            WallSpline spline,
            Vector3 pathPosition,
            Vector3 tangent,
            Transform parent,
            int index,
            float side)
        {
            WallSplineStyle style = spline.style;
            float extra = Mathf.Max(0f, style.buttressExtraDepth);
            float depth = style.wallThickness + extra;
            float height = Mathf.Max(0.2f, style.wallHeight * style.buttressHeightScale);
            Vector3 right = Vector3.Cross(Vector3.up, tangent.normalized).normalized;
            Vector3 position = pathPosition + right * (extra * 0.5f * side);
            position.y += height * 0.5f;

            CreateCube(
                "Buttress_" + index.ToString("000") + (side > 0f ? "_R" : "_L"),
                parent,
                position,
                Quaternion.LookRotation(tangent.normalized, Vector3.up),
                new Vector3(depth, height, style.buttressWidth),
                style.wallMaterial,
                spline.generateCollider);
        }

        private static void GenerateEndCaps(WallSpline spline, List<Vector3> path, Transform parent)
        {
            WallSplineStyle style = spline.style;
            if (spline.closedLoop || !style.generateEndCaps)
                return;

            float totalLength = CalculateLength(path);
            if (totalLength < 0.001f)
                return;

            GenerateEndCap(spline, path, 0f, parent, true);
            GenerateEndCap(spline, path, totalLength, parent, false);
        }

        private static void GenerateEndCap(
            WallSpline spline,
            List<Vector3> path,
            float distance,
            Transform parent,
            bool start)
        {
            Vector3 position;
            Vector3 tangent;
            if (!EvaluatePathAtDistance(path, distance, out position, out tangent))
                return;

            WallSplineStyle style = spline.style;
            float width = Mathf.Max(0.08f, style.endCapWidth);
            float extra = Mathf.Max(0f, style.endCapExtraDepth);
            float depth = style.wallThickness + extra * 2f;
            Vector3 inward = tangent.normalized * (start ? 1f : -1f);
            position += inward * (width * 0.5f);
            position.y += style.wallHeight * 0.5f;

            CreateCube(
                start ? "EndCap_Start" : "EndCap_End",
                parent,
                position,
                Quaternion.LookRotation(tangent.normalized, Vector3.up),
                new Vector3(depth, style.wallHeight, width),
                style.wallMaterial,
                spline.generateCollider);
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
                if (FindOpeningAtDistance(spline, distance, spacing * 0.2f) == null &&
                    Next01(random) <= style.topSpawnChance)
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

        private static WallSplineOpening FindOpeningAtDistance(WallSpline spline, float distance, float extraPadding)
        {
            if (spline.openings == null)
                return null;

            for (int i = 0; i < spline.openings.Count; i++)
            {
                WallSplineOpening opening = spline.openings[i];
                if (opening == null || !opening.enabled)
                    continue;

                float half = Mathf.Max(0.15f, opening.width * 0.5f) + Mathf.Max(0f, extraPadding);
                if (distance >= opening.distance - half && distance <= opening.distance + half)
                    return opening;
            }

            return null;
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
            float totalLength = CalculateLength(path);
            targetDistance = Mathf.Clamp(targetDistance, 0f, totalLength);
            float cursor = 0f;
            Vector3 lastValidTangent = Vector3.forward;

            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 a = path[i];
                Vector3 b = path[i + 1];
                Vector3 planar = new Vector3(b.x - a.x, 0f, b.z - a.z);
                float length = planar.magnitude;
                if (length < 0.001f)
                    continue;

                lastValidTangent = planar.normalized;
                if (cursor + length >= targetDistance - 0.0001f)
                {
                    float t = Mathf.Clamp01((targetDistance - cursor) / length);
                    position = Vector3.Lerp(a, b, t);
                    tangent = planar.normalized;
                    return true;
                }

                cursor += length;
            }

            if (path.Count > 0)
            {
                position = path[path.Count - 1];
                tangent = lastValidTangent;
                return true;
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
