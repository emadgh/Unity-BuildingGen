using System.Collections.Generic;
using UnityEngine;

namespace Emadgh.BuildingGen.Editor
{
    internal static class CustomFootprintValidationUtility
    {
        private const float Epsilon = 0.0001f;

        public static bool ValidateForBuild(
            BuildingGenerator generator,
            out string error)
        {
            error = null;

            if (!CustomFootprintMeshBuilder.TryGetSampledLoop(
                    generator,
                    out List<Vector2> outer,
                    out error))
            {
                return false;
            }

            List<Vector2> inner = OffsetPolygon(outer, generator.wallThickness);
            if (inner.Count != outer.Count || inner.Count < 3)
            {
                error = "Wall thickness could not produce a valid inner wall loop.";
                return false;
            }

            if (Mathf.Abs(SignedArea(inner)) < 0.1f)
            {
                error = "Wall thickness collapses the interior of the custom footprint.";
                return false;
            }

            if (HasSelfIntersection(inner))
            {
                error = "Wall thickness creates a self-intersecting inner wall loop. Reduce Wall Thickness or widen the tight concave corner.";
                return false;
            }

            for (int i = 0; i < inner.Count; i++)
            {
                if (!PointInPolygon(inner[i], outer))
                {
                    error = "A custom footprint corner is too tight for the current Wall Thickness. Reduce thickness or widen/soften that corner.";
                    return false;
                }
            }

            return true;
        }

        private static List<Vector2> OffsetPolygon(
            List<Vector2> polygon,
            float distance)
        {
            List<Vector2> result = new List<Vector2>(polygon.Count);
            float orientation = SignedArea(polygon) >= 0f ? 1f : -1f;

            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 previous = polygon[Wrap(i - 1, polygon.Count)];
                Vector2 current = polygon[i];
                Vector2 next = polygon[Wrap(i + 1, polygon.Count)];

                Vector2 previousDirection = (current - previous).normalized;
                Vector2 nextDirection = (next - current).normalized;
                Vector2 previousNormal =
                    new Vector2(-previousDirection.y, previousDirection.x)
                    * orientation;
                Vector2 nextNormal =
                    new Vector2(-nextDirection.y, nextDirection.x)
                    * orientation;

                Vector2 p1 = current + previousNormal * distance;
                Vector2 p2 = current + nextNormal * distance;

                if (TryLineIntersection(
                        p1,
                        previousDirection,
                        p2,
                        nextDirection,
                        out Vector2 intersection))
                {
                    float maxMiter = Mathf.Max(distance * 5f, 0.5f);
                    Vector2 delta = intersection - current;
                    if (delta.magnitude > maxMiter)
                        intersection = current + delta.normalized * maxMiter;

                    result.Add(intersection);
                }
                else
                {
                    Vector2 average = previousNormal + nextNormal;
                    if (average.sqrMagnitude < Epsilon)
                        average = previousNormal;
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

                    if (SegmentsIntersect(
                            loop[i],
                            loop[iNext],
                            loop[j],
                            loop[jNext]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool SegmentsIntersect(
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Vector2 d)
        {
            float o1 = Cross(b - a, c - a);
            float o2 = Cross(b - a, d - a);
            float o3 = Cross(d - c, a - c);
            float o4 = Cross(d - c, b - c);
            return o1 * o2 < -Epsilon && o3 * o4 < -Epsilon;
        }

        private static bool PointInPolygon(
            Vector2 point,
            List<Vector2> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1;
                 i < polygon.Count;
                 j = i++)
            {
                Vector2 pi = polygon[i];
                Vector2 pj = polygon[j];
                bool crosses =
                    ((pi.y > point.y) != (pj.y > point.y))
                    && point.x
                    < (pj.x - pi.x) * (point.y - pi.y)
                    / ((pj.y - pi.y) + 0.000001f)
                    + pi.x;

                if (crosses)
                    inside = !inside;
            }

            return inside;
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

        private static int Wrap(int index, int count)
        {
            int value = index % count;
            return value < 0 ? value + count : value;
        }
    }
}
