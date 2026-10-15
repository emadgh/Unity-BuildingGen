using System.Collections.Generic;
using UnityEngine;

namespace Emadgh.BuildingGen.Editor
{
    internal static class EditorMeshFactory
    {
        public static Mesh CreateBox(Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3[] v =
            {
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z),
                new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z),
                new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z)
            };

            int[] t =
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 4, 7, 0, 7, 3,
                1, 2, 6, 1, 6, 5,
                3, 7, 6, 3, 6, 2,
                0, 1, 5, 0, 5, 4
            };

            Mesh mesh = new Mesh();
            mesh.name = "BuildingGen_Box";
            mesh.vertices = v;
            mesh.triangles = t;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Creates a closed arch-shaped prism. Pivot is bottom-center; depth is along local Z.
        /// The 2D profile is convex, making it stable for the simple sample CSG cutters.
        /// </summary>
        public static Mesh CreateArchPrism(float width, float height, float depth, int archSegments)
        {
            width = Mathf.Max(0.1f, width);
            height = Mathf.Max(width * 0.5f + 0.05f, height);
            depth = Mathf.Max(0.05f, depth);
            archSegments = Mathf.Clamp(archSegments, 4, 32);

            float radius = width * 0.5f;
            float springY = height - radius;
            List<Vector2> profile = new List<Vector2>();
            profile.Add(new Vector2(-radius, 0f));
            profile.Add(new Vector2(radius, 0f));
            profile.Add(new Vector2(radius, springY));

            for (int i = 1; i <= archSegments; i++)
            {
                float angle = Mathf.Lerp(0f, Mathf.PI, i / (float)archSegments);
                profile.Add(new Vector2(Mathf.Cos(angle) * radius, springY + Mathf.Sin(angle) * radius));
            }

            return ExtrudeConvexProfile(profile, depth, "BuildingGen_ArchCutter");
        }

        private static Mesh ExtrudeConvexProfile(List<Vector2> profile, float depth, string name)
        {
            int count = profile.Count;
            float hz = depth * 0.5f;
            Vector3[] vertices = new Vector3[count * 2];

            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(profile[i].x, profile[i].y, -hz);
                vertices[i + count] = new Vector3(profile[i].x, profile[i].y, hz);
            }

            List<int> triangles = new List<int>();

            // Back cap.
            for (int i = 1; i < count - 1; i++)
            {
                triangles.Add(0);
                triangles.Add(i + 1);
                triangles.Add(i);
            }

            // Front cap.
            for (int i = 1; i < count - 1; i++)
            {
                triangles.Add(count);
                triangles.Add(count + i);
                triangles.Add(count + i + 1);
            }

            // Side band.
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                int a = i;
                int b = next;
                int c = next + count;
                int d = i + count;

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(d);
            }

            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
