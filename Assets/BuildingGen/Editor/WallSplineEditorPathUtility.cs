using System.Collections.Generic;
using UnityEngine;

namespace Emadgh.BuildingGen.Editor
{
    /// <summary>
    /// SceneView/Inspector path queries may happen while the previously generated wall is still
    /// present. Terrain-conform raycasts must not hit that wall's own colliders. Generation itself
    /// is already safe because WallSplineBuilder clears the old generated root before sampling.
    /// </summary>
    internal static class WallSplineEditorPathUtility
    {
        public static List<Vector3> GetSampledPath(WallSpline spline)
        {
            Collider[] generatedColliders = null;
            bool[] enabledStates = null;

            if (spline != null && spline.generatedRoot != null)
            {
                generatedColliders = spline.generatedRoot.GetComponentsInChildren<Collider>(true);
                enabledStates = new bool[generatedColliders.Length];

                for (int i = 0; i < generatedColliders.Length; i++)
                {
                    Collider collider = generatedColliders[i];
                    if (collider == null)
                        continue;

                    enabledStates[i] = collider.enabled;
                    collider.enabled = false;
                }
            }

            try
            {
                Physics.SyncTransforms();
                return WallSplineBuilder.GetSampledPath(spline);
            }
            finally
            {
                if (generatedColliders != null)
                {
                    for (int i = 0; i < generatedColliders.Length; i++)
                    {
                        Collider collider = generatedColliders[i];
                        if (collider != null)
                            collider.enabled = enabledStates[i];
                    }

                    Physics.SyncTransforms();
                }
            }
        }

        public static float GetPathLength(WallSpline spline)
        {
            List<Vector3> path = GetSampledPath(spline);
            float total = 0f;

            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 delta = path[i + 1] - path[i];
                delta.y = 0f;
                total += delta.magnitude;
            }

            return total;
        }
    }
}
