using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emadgh.BuildingGen.Editor
{
    /// <summary>
    /// Builds the SolidMesh body from an editable closed first-floor footprint.
    /// Control points live in the BuildingGenerator local XZ plane.
    /// </summary>
    internal static class CustomFootprintMeshBuilder
    {
        private const float Epsilon = 0.0001f;

        private sealed class MeshDraft
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> wallTriangles = new List<int>();
            private readonly List<int> roofTriangles = new List<int>();

            public void AddTriangle(
                Vector3 a,
                Vector3 b,
                Vector3 c,
                Vector3 desiredNormal,
                bool roof)
            {
                Vector3 normal = desiredNormal.sqrMagnitude > Epsilon
                    ? desiredNormal.normalized
                    : Vector3.up;

                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f)
                {
                    Vector3 swap = b;
                    b = c;
                    c = swap;
                }

                int baseIndex = vertices.Count;
                AddVertex(a, normal, PlanarUv(a, normal));
                AddVertex(b, normal, PlanarUv(b, normal));
                AddVertex(c, normal, PlanarUv(c, normal));

                List<int> target = roof ? roofTriangles : wallTriangles;
                target.Add(baseIndex);
                target.Add(baseIndex + 1);
                target.Add(baseIndex + 2);
            }

            public void AddQuad(
                Vector3 a,
                Vector3 b,
                Vector3 c,
                Vector3 d,
                Vector3 desiredNormal,
                bool roof)
            {
                Vector3 normal = desiredNormal.sqrMagnitude > Epsilon
                    ? desiredNormal.normalized
                    : Vector3.up;

                Vector3 horizontal = b - a;
                horizontal.y = 0f;
                if (horizontal.sqrMagnitude < Epsilon)
                {
                    horizontal = d - a;
                    horizontal.y = 0f;
                }

                horizontal = horizontal.sqrMagnitude > Epsilon
                    ? horizontal.normalized
                    : Vector3.right;

                int baseIndex = vertices.Count;
                AddVertex(a, normal, FaceUv(a, a, horizontal, normal));
                AddVertex(b, normal, FaceUv(b, a, horizontal, normal));
                AddVertex(c, normal, FaceUv(c, a, horizontal, normal));
                AddVertex(d, normal, FaceUv(d, a, horizontal, normal));

                List<int> target = roof ? roofTriangles : wallTriangles;
                bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f;

                if (!flip)
                {
                    target.Add(baseIndex);
                    target.Add(baseIndex + 1);
                    target.Add(baseIndex + 2);
                    target.Add(baseIndex);
                    target.Add(baseIndex + 2);
                    target.Add(baseIndex + 3);
                }
                else
                {
                    target.Add(baseIndex);
                    target.Add(baseIndex + 2);
                    target.Add(baseIndex + 1);
                    target.Add(baseIndex);
                    target.Add(baseIndex + 3);
                    target.Add(baseIndex + 2);
                }
            }

            public Mesh ToMesh(string name)
            {
                Mesh mesh = new Mesh();
                mesh.name = name;
                if (vertices.Count > 65535)
                    mesh.indexFormat = IndexFormat.UInt32;

                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uv);
                mesh.SetColors(colors);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(wallTriangles, 0, true);
                mesh.SetTriangles(roofTriangles, 1, true);
                mesh.RecalculateBounds();
                return mesh;
            }

            private void AddVertex(Vector3 position, Vector3 normal, Vector2 textureUv)
            {
                vertices.Add(position);
                normals.Add(normal);
                uv.Add(textureUv);
                colors.Add(Color.white);
            }

            private static Vector2 FaceUv(
                Vector3 position,
                Vector3 origin,
                Vector3 horizontal,
                Vector3 normal)
            {
                const float scale = 0.5f;
                if (Mathf.Abs(normal.y) > 0.75f)
                    return new Vector2(position.x, position.z) * scale;

                float u = Vector3.Dot(position - origin, horizontal);
                return new Vector2(u, position.y) * scale;
            }

            private static Vector2 PlanarUv(Vector3 position, Vector3 normal)
            {
                const float scale = 0.5f;
                if (Mathf.Abs(normal.y) > 0.75f)
                    return new Vector2(position.x, position.z) * scale;

                if (Mathf.Abs(normal.x) > Mathf.Abs(normal.z))
                    return new Vector2(position.z, position.y) * scale;

                return new Vector2(position.x, position.y) * scale;
            }
        }

        public static Mesh Build(
            BuildingGenerator generator,
            out List<SolidFacadeDescriptor> facades,
            out string error)
        {
            facades = new List<SolidFacadeDescriptor>();
            error = null;

            if (!TryGetSampledLoop(generator, out List<Vector2> outer, out error))
                return null;

            EnsureCounterClockwise(outer);

            List<Vector2> roomFace = OffsetPolygon(outer, generator.wallThickness);
            if (!ValidateLoop(roomFace, generator.wallThickness * 0.5f, out string roomError))
            {
                error = "Wall thickness collapses the custom footprint: " + roomError;
                return null;
            }

            float totalHeight = Mathf.Max(0.2f, generator.TotalHeight);
            bool generateRoof = generator.style != null && generator.style.generateFlatRoof;
            float roofThickness = generateRoof
                ? Mathf.Clamp(
                    generator.style.roofThickness,
                    0.05f,
                    Mathf.Max(0.05f, totalHeight * 0.35f))
                : 0f;
            float ceilingHeight = generateRoof
                ? Mathf.Max(0.15f, totalHeight - roofThickness)
                : totalHeight;

            MeshDraft draft = new MeshDraft();
            BuildWallShell(
                draft,
                outer,
                roomFace,
                totalHeight,
                ceilingHeight,
                !generateRoof,
                facades);

            if (generateRoof)
            {
                AddPolygonCap(draft, outer, totalHeight, Vector3.up, true);
                AddPolygonCap(draft, roomFace, ceilingHeight, Vector3.down, true);
            }

            return draft.ToMesh("BuildingGen_CustomFootprintSolid");
        }

        public static bool TryGetSampledLoop(
            BuildingGenerator generator,
            out List<Vector2> loop,
            out string error)
        {
            loop = new List<Vector2>();
            error = null;

            if (generator == null || generator.customFootprintPoints == null)
            {
                error = "Custom footprint has no point list.";
                return false;
            }

            List<Vector2> source = CleanupPoints(generator.customFootprintPoints);
            if (source.Count < 3)
            {
                error = "Custom footprint needs at least three unique control points.";
                return false;
            }

            if (generator.customFootprintInterpolation == FootprintSplineInterpolation.Polyline)
            {
                loop.AddRange(source);
            }
            else
            {
                int subdivisions = Mathf.Clamp(generator.customFootprintSubdivisions, 1, 16);
                for (int segment = 0; segment < source.Count; segment++)
                {
                    Vector2 p0 = source[Wrap(segment - 1, source.Count)];
                    Vector2 p1 = source[segment];
                    Vector2 p2 = source[Wrap(segment + 1, source.Count)];
                    Vector2 p3 = source[Wrap(segment + 2, source.Count)];

                    if (segment == 0)
                        loop.Add(p1);

                    for (int step = 1; step <= subdivisions; step++)
                    {
                        // The last sample of every segment is the next control point.
                        // Avoid duplicating the first point at the final segment.
                        if (segment == source.Count - 1 && step == subdivisions)
                            continue;

                        float t = step / (float)subdivisions;
                        loop.Add(CatmullRom(p0, p1, p2, p3, t));
                    }
                }

                loop = CleanupPoints(loop);
            }

            if (!ValidateLoop(loop, 0.05f, out error))
                return false;

            EnsureCounterClockwise(loop);
            return true;
        }

        public static bool ContainsPoint(BuildingGenerator generator, Vector2 point)
        {
            if (!TryGetSampledLoop(generator, out List<Vector2> loop, out string _))
                return false;
            return PointInPolygon(point, loop);
        }

        public static bool ValidateControlPolygon(BuildingGenerator generator, out string error)
        {
            return TryGetSampledLoop(generator, out List<Vector2> _, out error);
        }

        private static void BuildWallShell(
            MeshDraft draft,
            List<Vector2> facadeLoop,
            List<Vector2> roomLoop,
            float facadeTop,
            float roomTop,
            bool capTop,
            List<SolidFacadeDescriptor> facades)
        {
            int count = facadeLoop.Count;
            if (roomLoop.Count != count)
                return;

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                Vector2 a2 = facadeLoop[i];
                Vector2 b2 = facadeLoop[next];
                Vector2 ia2 = roomLoop[i];
                Vector2 ib2 = roomLoop[next];

                Vector2 direction = (b2 - a2).normalized;
                Vector2 outward = new Vector2(direction.y, -direction.x);
                Vector3 facadeNormal = XzDirection(outward);

                Vector3 a0 = Xz(a2, 0f);
                Vector3 b0 = Xz(b2, 0f);
                Vector3 aTop = Xz(a2, facadeTop);
                Vector3 bTop = Xz(b2, facadeTop);
                Vector3 ia0 = Xz(ia2, 0f);
                Vector3 ib0 = Xz(ib2, 0f);
                Vector3 iaTop = Xz(ia2, roomTop);
                Vector3 ibTop = Xz(ib2, roomTop);

                draft.AddQuad(a0, b0, bTop, aTop, facadeNormal, false);
                draft.AddQuad(ib0, ia0, iaTop, ibTop, -facadeNormal, false);
                draft.AddQuad(a0, ia0, ib0, b0, Vector3.down, false);

                if (capTop)
                    draft.AddQuad(aTop, bTop, ibTop, iaTop, Vector3.up, false);

                facades.Add(new SolidFacadeDescriptor
                {
                    start = a2,
                    end = b2,
                    outward = outward,
                    role = ClassifyFacade(outward),
                    courtyard = false
                });
            }
        }

        private static void AddPolygonCap(
            MeshDraft draft,
            List<Vector2> polygon,
            float y,
            Vector3 normal,
            bool roof)
        {
            List<int> triangles = TriangulatePolygon(polygon);
            for (int i = 0; i < triangles.Count; i += 3)
            {
                draft.AddTriangle(
                    Xz(polygon[triangles[i]], y),
                    Xz(polygon[triangles[i + 1]], y),
                    Xz(polygon[triangles[i + 2]], y),
                    normal,
                    roof);
            }
        }

        /// <summary>
        /// Positive distance offsets a CCW polygon inward.
        /// </summary>
        private static List<Vector2> OffsetPolygon(List<Vector2> polygon, float distance)
        {
            List<Vector2> result = new List<Vector2>(polygon.Count);
            float orientation = SignedArea(polygon) >= 0f ? 1f : -1f;

            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 prev = polygon[Wrap(i - 1, polygon.Count)];
                Vector2 current = polygon[i];
                Vector2 next = polygon[Wrap(i + 1, polygon.Count)];

                Vector2 d1 = (current - prev).normalized;
                Vector2 d2 = (next - current).normalized;
                Vector2 n1 = new Vector2(-d1.y, d1.x) * orientation;
                Vector2 n2 = new Vector2(-d2.y, d2.x) * orientation;

                Vector2 p1 = current + n1 * distance;
                Vector2 p2 = current + n2 * distance;

                if (TryLineIntersection(p1, d1, p2, d2, out Vector2 intersection))
                {
                    float maxMiter = Mathf.Max(distance * 5f, 0.5f);
                    Vector2 delta = intersection - current;
                    if (delta.magnitude > maxMiter)
                        intersection = current + delta.normalized * maxMiter;
                    result.Add(intersection);
                }
                else
                {
                    Vector2 average = n1 + n2;
                    if (average.sqrMagnitude < Epsilon)
                        average = n1;
                    result.Add(current + average.normalized * distance);
                }
            }

            return result;
        }

        private static bool TryLineIntersection(
            Vector2 p,
            Vector2 r,
            Vector2 q,
            Vector2 s,
            out Vector2 intersection)
        {
            float cross = Cross(r, s);
            if (Mathf.Abs(cross) < Epsilon)
            {
                intersection = Vector2.zero;
                return false;
            }

            float t = Cross(q - p, s) / cross;
            intersection = p + r * t;
            return true;
        }

        private static List<int> TriangulatePolygon(List<Vector2> polygon)
        {
            List<int> result = new List<int>();
            if (polygon == null || polygon.Count < 3)
                return result;

            List<int> indices = new List<int>();
            for (int i = 0; i < polygon.Count; i++)
                indices.Add(i);

            bool ccw = SignedArea(polygon) > 0f;
            int guard = 0;
            while (indices.Count > 3 && guard++ < polygon.Count * polygon.Count * 2)
            {
                bool clipped = false;
                for (int i = 0; i < indices.Count; i++)
                {
                    int i0 = indices[Wrap(i - 1, indices.Count)];
                    int i1 = indices[i];
                    int i2 = indices[Wrap(i + 1, indices.Count)];

                    Vector2 a = polygon[i0];
                    Vector2 b = polygon[i1];
                    Vector2 c = polygon[i2];
                    float cross = Cross(b - a, c - b);
                    if (ccw ? cross <= Epsilon : cross >= -Epsilon)
                        continue;

                    bool contains = false;
                    for (int j = 0; j < indices.Count; j++)
                    {
                        int test = indices[j];
                        if (test == i0 || test == i1 || test == i2)
                            continue;

                        if (PointInTriangle(polygon[test], a, b, c))
                        {
                            contains = true;
                            break;
                        }
                    }

                    if (contains)
                        continue;

                    if (ccw)
                    {
                        result.Add(i0);
                        result.Add(i1);
                        result.Add(i2);
                    }
                    else
                    {
                        result.Add(i2);
                        result.Add(i1);
                        result.Add(i0);
                    }

                    indices.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (!clipped)
                    break;
            }

            if (indices.Count == 3)
            {
                if (ccw)
                {
                    result.Add(indices[0]);
                    result.Add(indices[1]);
                    result.Add(indices[2]);
                }
                else
                {
                    result.Add(indices[2]);
                    result.Add(indices[1]);
                    result.Add(indices[0]);
                }
            }

            return result;
        }

        private static bool ValidateLoop(List<Vector2> loop, float minimumEdge, out string error)
        {
            error = null;
            if (loop == null || loop.Count < 3)
            {
                error = "Need at least three unique points.";
                return false;
            }

            if (Mathf.Abs(SignedArea(loop)) < 0.1f)
            {
                error = "Footprint area is too small or nearly collinear.";
                return false;
            }

            for (int i = 0; i < loop.Count; i++)
            {
                int next = (i + 1) % loop.Count;
                if (Vector2.Distance(loop[i], loop[next]) < minimumEdge)
                {
                    error = "Two neighboring footprint samples are too close.";
                    return false;
                }
            }

            if (HasSelfIntersection(loop))
            {
                error = "Footprint intersects itself. Move or reorder the control points.";
                return false;
            }

            return true;
        }

        private static bool HasSelfIntersection(List<Vector2> loop)
        {
            int count = loop.Count;
            for (int i = 0; i < count; i++)
            {
                int iNext = (i + 1) % count;
                for (int j = i + 1; j < count; j++)
                {
                    int jNext = (j + 1) % count;
                    if (i == j || iNext == j || jNext == i)
                        continue;
                    if (i == 0 && jNext == 0)
                        continue;

                    if (SegmentsIntersect(loop[i], loop[iNext], loop[j], loop[jNext]))
                        return true;
                }
            }
            return false;
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float o1 = Cross(b - a, c - a);
            float o2 = Cross(b - a, d - a);
            float o3 = Cross(d - c, a - c);
            float o4 = Cross(d - c, b - c);
            return o1 * o2 < -Epsilon && o3 * o4 < -Epsilon;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float c1 = Cross(b - a, p - a);
            float c2 = Cross(c - b, p - b);
            float c3 = Cross(a - c, p - c);
            bool hasNeg = c1 < -Epsilon || c2 < -Epsilon || c3 < -Epsilon;
            bool hasPos = c1 > Epsilon || c2 > Epsilon || c3 > Epsilon;
            return !(hasNeg && hasPos);
        }

        private static bool PointInPolygon(Vector2 point, List<Vector2> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 pi = polygon[i];
                Vector2 pj = polygon[j];
                bool intersect = ((pi.y > point.y) != (pj.y > point.y))
                    && point.x < (pj.x - pi.x) * (point.y - pi.y)
                    / ((pj.y - pi.y) + 0.000001f) + pi.x;
                if (intersect)
                    inside = !inside;
            }
            return inside;
        }

        private static List<Vector2> CleanupPoints(List<Vector2> source)
        {
            List<Vector2> result = new List<Vector2>();
            for (int i = 0; i < source.Count; i++)
            {
                Vector2 point = source[i];
                if (result.Count == 0 || Vector2.Distance(result[result.Count - 1], point) > 0.001f)
                    result.Add(point);
            }

            if (result.Count > 2 && Vector2.Distance(result[0], result[result.Count - 1]) < 0.001f)
                result.RemoveAt(result.Count - 1);
            return result;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1)
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static SolidFacadeRole ClassifyFacade(Vector2 outward)
        {
            if (outward.y > 0.55f)
                return SolidFacadeRole.Front;
            if (outward.y < -0.55f)
                return SolidFacadeRole.Back;
            return SolidFacadeRole.Side;
        }

        private static Vector3 Xz(Vector2 point, float y)
        {
            return new Vector3(point.x, y, point.y);
        }

        private static Vector3 XzDirection(Vector2 direction)
        {
            return new Vector3(direction.x, 0f, direction.y);
        }

        private static float SignedArea(List<Vector2> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }

        private static void EnsureCounterClockwise(List<Vector2> polygon)
        {
            if (SignedArea(polygon) < 0f)
                polygon.Reverse();
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static int Wrap(int index, int count)
        {
            int value = index % count;
            return value < 0 ? value + count : value;
        }
    }
}
