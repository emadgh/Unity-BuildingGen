using System.Collections.Generic;
using UnityEngine;

namespace Emadgh.BuildingGen.Editor
{
    internal struct SolidFacadeDescriptor
    {
        public Vector2 start;
        public Vector2 end;
        public Vector2 outward;
        public SolidFacadeRole role;
        public bool courtyard;

        public float Width
        {
            get { return Vector2.Distance(start, end); }
        }

        public Vector2 Midpoint
        {
            get { return (start + end) * 0.5f; }
        }
    }

    /// <summary>
    /// Produces the non-modular building body used by the v0.6 solid backend.
    /// The output is one Mesh containing continuous perimeter wall solids plus the roof slab.
    /// Facades are returned only as logical metadata for opening placement.
    /// </summary>
    internal static class SolidBuildingMeshBuilder
    {
        private sealed class MeshDraft
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<Color> colors = new List<Color>();
            public readonly List<int> wallTriangles = new List<int>();
            public readonly List<int> roofTriangles = new List<int>();

            public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 desiredNormal, bool roof)
            {
                Vector3 normal = desiredNormal.sqrMagnitude > 0.0001f ? desiredNormal.normalized : Vector3.up;
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

                List<int> triangles = roof ? roofTriangles : wallTriangles;
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
            }

            public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 desiredNormal, bool roof)
            {
                Vector3 normal = desiredNormal.sqrMagnitude > 0.0001f ? desiredNormal.normalized : Vector3.up;

                Vector3 horizontal = b - a;
                horizontal.y = 0f;
                if (horizontal.sqrMagnitude < 0.0001f)
                {
                    horizontal = d - a;
                    horizontal.y = 0f;
                }
                horizontal = horizontal.sqrMagnitude > 0.0001f ? horizontal.normalized : Vector3.right;

                int baseIndex = vertices.Count;
                AddVertex(a, normal, FaceUv(a, a, horizontal, normal));
                AddVertex(b, normal, FaceUv(b, a, horizontal, normal));
                AddVertex(c, normal, FaceUv(c, a, horizontal, normal));
                AddVertex(d, normal, FaceUv(d, a, horizontal, normal));

                List<int> triangles = roof ? roofTriangles : wallTriangles;
                bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f;
                if (!flip)
                {
                    triangles.Add(baseIndex);
                    triangles.Add(baseIndex + 1);
                    triangles.Add(baseIndex + 2);
                    triangles.Add(baseIndex);
                    triangles.Add(baseIndex + 2);
                    triangles.Add(baseIndex + 3);
                }
                else
                {
                    triangles.Add(baseIndex);
                    triangles.Add(baseIndex + 2);
                    triangles.Add(baseIndex + 1);
                    triangles.Add(baseIndex);
                    triangles.Add(baseIndex + 3);
                    triangles.Add(baseIndex + 2);
                }
            }

            private void AddVertex(Vector3 position, Vector3 normal, Vector2 textureUv)
            {
                vertices.Add(position);
                normals.Add(normal);
                uv.Add(textureUv);
                colors.Add(Color.white);
            }

            private static Vector2 FaceUv(Vector3 position, Vector3 origin, Vector3 horizontal, Vector3 normal)
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

            public Mesh ToMesh(string name)
            {
                Mesh mesh = new Mesh();
                mesh.name = name;
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
        }

        public static Mesh Build(BuildingGenerator generator, out List<SolidFacadeDescriptor> facades)
        {
            facades = new List<SolidFacadeDescriptor>();

            List<Vector2> outer = BuildOuterFootprint(generator);
            EnsureCounterClockwise(outer);

            float totalHeight = Mathf.Max(0.2f, generator.TotalHeight);
            bool generateRoof = generator.style != null && generator.style.generateFlatRoof;
            float roofThickness = generateRoof
                ? Mathf.Clamp(generator.style.roofThickness, 0.05f, Mathf.Max(0.05f, totalHeight * 0.35f))
                : 0f;
            float wallTop = generateRoof ? Mathf.Max(0.15f, totalHeight - roofThickness) : totalHeight;

            MeshDraft draft = new MeshDraft();

            List<Vector2> outerInner = OffsetPolygon(outer, generator.wallThickness);
            BuildWallRing(
                draft,
                outer,
                outerInner,
                0f,
                wallTop,
                true,
                !generateRoof,
                false,
                facades);

            List<Vector2> courtyard = null;
            if (generator.footprint == BuildingFootprint.Courtyard)
            {
                courtyard = BuildCourtyardFootprint(generator);
                EnsureCounterClockwise(courtyard);

                List<Vector2> courtyardRoomFace = OffsetPolygon(courtyard, -generator.wallThickness);
                BuildWallRing(
                    draft,
                    courtyard,
                    courtyardRoomFace,
                    0f,
                    wallTop,
                    true,
                    !generateRoof,
                    true,
                    facades);
            }

            if (generateRoof)
            {
                if (courtyard != null)
                    AddRectangularRingPrism(draft, outer, courtyard, wallTop, totalHeight);
                else
                    AddPolygonPrism(draft, outer, wallTop, totalHeight);
            }

            Mesh mesh = draft.ToMesh("BuildingGen_SolidBuilding");
            return mesh;
        }

        internal static List<Vector2> BuildOuterFootprint(BuildingGenerator generator)
        {
            float hw = generator.width * 0.5f;
            float hd = generator.depth * 0.5f;
            float wing = Mathf.Clamp(generator.wingWidth, 1.5f, Mathf.Min(generator.width, generator.depth) * 0.45f);

            if (generator.footprint == BuildingFootprint.LShape)
            {
                return new List<Vector2>
                {
                    new Vector2(-hw, -hd),
                    new Vector2(hw, -hd),
                    new Vector2(hw, -hd + wing),
                    new Vector2(-hw + wing, -hd + wing),
                    new Vector2(-hw + wing, hd),
                    new Vector2(-hw, hd)
                };
            }

            if (generator.footprint == BuildingFootprint.UShape)
            {
                float recess = Mathf.Clamp(generator.uRecessDepth, 1.5f, generator.depth - generator.wallThickness * 4f);
                float innerBackZ = hd - recess;
                return new List<Vector2>
                {
                    new Vector2(-hw, -hd),
                    new Vector2(hw, -hd),
                    new Vector2(hw, hd),
                    new Vector2(hw - wing, hd),
                    new Vector2(hw - wing, innerBackZ),
                    new Vector2(-hw + wing, innerBackZ),
                    new Vector2(-hw + wing, hd),
                    new Vector2(-hw, hd)
                };
            }

            return new List<Vector2>
            {
                new Vector2(-hw, -hd),
                new Vector2(hw, -hd),
                new Vector2(hw, hd),
                new Vector2(-hw, hd)
            };
        }

        internal static List<Vector2> BuildCourtyardFootprint(BuildingGenerator generator)
        {
            float cw = Mathf.Min(generator.courtyardWidth, generator.width - generator.wallThickness * 6f);
            float cd = Mathf.Min(generator.courtyardDepth, generator.depth - generator.wallThickness * 6f);
            float hw = cw * 0.5f;
            float hd = cd * 0.5f;

            return new List<Vector2>
            {
                new Vector2(-hw, -hd),
                new Vector2(hw, -hd),
                new Vector2(hw, hd),
                new Vector2(-hw, hd)
            };
        }

        internal static bool ContainsFootprintPoint(BuildingGenerator generator, Vector2 point)
        {
            List<Vector2> outer = BuildOuterFootprint(generator);
            EnsureCounterClockwise(outer);
            if (!PointInPolygon(point, outer))
                return false;

            if (generator.footprint == BuildingFootprint.Courtyard)
            {
                List<Vector2> courtyard = BuildCourtyardFootprint(generator);
                EnsureCounterClockwise(courtyard);
                if (PointInPolygon(point, courtyard))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Builds one closed perimeter wall solid between facadeLoop and roomLoop.
        /// Both loops have the same point count and matching vertices.
        /// </summary>
        private static void BuildWallRing(
            MeshDraft draft,
            List<Vector2> facadeLoop,
            List<Vector2> roomLoop,
            float y0,
            float y1,
            bool capBottom,
            bool capTop,
            bool courtyard,
            List<SolidFacadeDescriptor> facades)
        {
            int count = facadeLoop.Count;
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                Vector2 a2 = facadeLoop[i];
                Vector2 b2 = facadeLoop[next];
                Vector2 ia2 = roomLoop[i];
                Vector2 ib2 = roomLoop[next];

                Vector2 direction = (b2 - a2).normalized;
                Vector2 right = new Vector2(direction.y, -direction.x);
                Vector2 outward = courtyard ? -right : right;

                Vector3 a0 = Xz(a2, y0);
                Vector3 b0 = Xz(b2, y0);
                Vector3 b1 = Xz(b2, y1);
                Vector3 a1 = Xz(a2, y1);

                Vector3 ia0 = Xz(ia2, y0);
                Vector3 ib0 = Xz(ib2, y0);
                Vector3 ib1 = Xz(ib2, y1);
                Vector3 ia1 = Xz(ia2, y1);

                Vector3 facadeNormal = XzDirection(outward);
                draft.AddQuad(a0, b0, b1, a1, facadeNormal, false);
                draft.AddQuad(ib0, ia0, ia1, ib1, -facadeNormal, false);

                if (capBottom)
                    draft.AddQuad(a0, ia0, ib0, b0, Vector3.down, false);
                if (capTop)
                    draft.AddQuad(a1, b1, ib1, ia1, Vector3.up, false);

                facades.Add(new SolidFacadeDescriptor
                {
                    start = a2,
                    end = b2,
                    outward = outward,
                    role = courtyard ? SolidFacadeRole.Courtyard : ClassifyFacade(outward),
                    courtyard = courtyard
                });
            }
        }

        private static void AddPolygonPrism(MeshDraft draft, List<Vector2> polygon, float y0, float y1)
        {
            List<int> triangles = TriangulatePolygon(polygon);
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector2 a = polygon[triangles[i]];
                Vector2 b = polygon[triangles[i + 1]];
                Vector2 c = polygon[triangles[i + 2]];

                draft.AddTriangle(Xz(a, y1), Xz(b, y1), Xz(c, y1), Vector3.up, true);
                draft.AddTriangle(Xz(a, y0), Xz(c, y0), Xz(b, y0), Vector3.down, true);
            }

            for (int i = 0; i < polygon.Count; i++)
            {
                int next = (i + 1) % polygon.Count;
                Vector2 a2 = polygon[i];
                Vector2 b2 = polygon[next];
                Vector2 dir = (b2 - a2).normalized;
                Vector2 outward = new Vector2(dir.y, -dir.x);

                draft.AddQuad(
                    Xz(a2, y0),
                    Xz(b2, y0),
                    Xz(b2, y1),
                    Xz(a2, y1),
                    XzDirection(outward),
                    false);
            }
        }

        private static void AddRectangularRingPrism(
            MeshDraft draft,
            List<Vector2> outer,
            List<Vector2> inner,
            float y0,
            float y1)
        {
            if (outer.Count != 4 || inner.Count != 4)
            {
                AddPolygonPrism(draft, outer, y0, y1);
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;

                Vector3 oa0 = Xz(outer[i], y0);
                Vector3 ob0 = Xz(outer[next], y0);
                Vector3 ob1 = Xz(outer[next], y1);
                Vector3 oa1 = Xz(outer[i], y1);

                Vector3 ia0 = Xz(inner[i], y0);
                Vector3 ib0 = Xz(inner[next], y0);
                Vector3 ib1 = Xz(inner[next], y1);
                Vector3 ia1 = Xz(inner[i], y1);

                Vector2 outerDir = (outer[next] - outer[i]).normalized;
                Vector2 outerNormal = new Vector2(outerDir.y, -outerDir.x);
                Vector2 innerDir = (inner[next] - inner[i]).normalized;
                Vector2 courtyardNormal = new Vector2(-innerDir.y, innerDir.x);

                draft.AddQuad(oa1, ob1, ib1, ia1, Vector3.up, true);
                draft.AddQuad(oa0, ia0, ib0, ob0, Vector3.down, true);
                draft.AddQuad(oa0, ob0, ob1, oa1, XzDirection(outerNormal), false);
                draft.AddQuad(ib0, ia0, ia1, ib1, XzDirection(courtyardNormal), false);
            }
        }

        /// <summary>
        /// Positive distance offsets a CCW polygon inward. Negative values expand it.
        /// This mitered line-intersection offset is appropriate for the orthogonal footprints used by BuildingGen.
        /// </summary>
        private static List<Vector2> OffsetPolygon(List<Vector2> polygon, float distance)
        {
            List<Vector2> result = new List<Vector2>(polygon.Count);
            float area = SignedArea(polygon);
            float orientation = area >= 0f ? 1f : -1f;

            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 previous = polygon[(i - 1 + polygon.Count) % polygon.Count];
                Vector2 current = polygon[i];
                Vector2 next = polygon[(i + 1) % polygon.Count];

                Vector2 prevDir = (current - previous).normalized;
                Vector2 nextDir = (next - current).normalized;

                Vector2 prevLeft = new Vector2(-prevDir.y, prevDir.x) * orientation;
                Vector2 nextLeft = new Vector2(-nextDir.y, nextDir.x) * orientation;

                Vector2 p1 = current + prevLeft * distance;
                Vector2 p2 = current + nextLeft * distance;

                Vector2 intersection;
                if (TryLineIntersection(p1, prevDir, p2, nextDir, out intersection))
                {
                    float maxMiter = Mathf.Max(Mathf.Abs(distance) * 8f, 0.25f);
                    if ((intersection - current).magnitude <= maxMiter)
                    {
                        result.Add(intersection);
                        continue;
                    }
                }

                Vector2 average = prevLeft + nextLeft;
                if (average.sqrMagnitude < 0.0001f)
                    average = nextLeft;
                average.Normalize();
                result.Add(current + average * distance);
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
            if (Mathf.Abs(cross) < 0.00001f)
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
            bool ccw = SignedArea(polygon) >= 0f;
            if (ccw)
            {
                for (int i = 0; i < polygon.Count; i++)
                    indices.Add(i);
            }
            else
            {
                for (int i = polygon.Count - 1; i >= 0; i--)
                    indices.Add(i);
            }

            int guard = polygon.Count * polygon.Count;
            while (indices.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < indices.Count; i++)
                {
                    int prev = indices[(i - 1 + indices.Count) % indices.Count];
                    int current = indices[i];
                    int next = indices[(i + 1) % indices.Count];

                    Vector2 a = polygon[prev];
                    Vector2 b = polygon[current];
                    Vector2 c = polygon[next];

                    if (Cross(b - a, c - b) <= 0.000001f)
                        continue;

                    bool containsPoint = false;
                    for (int j = 0; j < indices.Count; j++)
                    {
                        int test = indices[j];
                        if (test == prev || test == current || test == next)
                            continue;

                        if (PointInTriangle(polygon[test], a, b, c))
                        {
                            containsPoint = true;
                            break;
                        }
                    }

                    if (containsPoint)
                        continue;

                    result.Add(prev);
                    result.Add(current);
                    result.Add(next);
                    indices.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (!clipped)
                    break;
            }

            if (indices.Count == 3)
            {
                result.Add(indices[0]);
                result.Add(indices[1]);
                result.Add(indices[2]);
            }

            return result;
        }

        private static bool PointInPolygon(Vector2 point, List<Vector2> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 pi = polygon[i];
                Vector2 pj = polygon[j];
                bool crosses = ((pi.y > point.y) != (pj.y > point.y))
                    && (point.x < (pj.x - pi.x) * (point.y - pi.y)
                        / Mathf.Max(0.000001f, pj.y - pi.y) + pi.x);
                if (crosses)
                    inside = !inside;
            }
            return inside;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float c1 = Cross(b - a, p - a);
            float c2 = Cross(c - b, p - b);
            float c3 = Cross(a - c, p - c);
            const float epsilon = -0.00001f;
            return c1 >= epsilon && c2 >= epsilon && c3 >= epsilon;
        }

        private static SolidFacadeRole ClassifyFacade(Vector2 outward)
        {
            if (outward.y > 0.55f)
                return SolidFacadeRole.Front;
            if (outward.y < -0.55f)
                return SolidFacadeRole.Back;
            return SolidFacadeRole.Side;
        }

        private static void EnsureCounterClockwise(List<Vector2> polygon)
        {
            if (SignedArea(polygon) < 0f)
                polygon.Reverse();
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

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static Vector3 Xz(Vector2 value, float y)
        {
            return new Vector3(value.x, y, value.y);
        }

        private static Vector3 XzDirection(Vector2 value)
        {
            return new Vector3(value.x, 0f, value.y).normalized;
        }
    }
}
