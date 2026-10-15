using UnityEngine;

#if PROBUILDER_EXPERIMENTAL_FEATURES
using System.Linq;
using UnityEngine.ProBuilder.Csg;
#endif

namespace Emadgh.BuildingGen.Editor
{
    /// <summary>
    /// Isolates the generator from the concrete CSG implementation.
    /// v0.1 uses ProBuilder's experimental CSG when it is enabled.
    /// </summary>
    internal static class ProBuilderBooleanBackend
    {
        public static bool IsAvailable
        {
            get
            {
#if PROBUILDER_EXPERIMENTAL_FEATURES
                return true;
#else
                return false;
#endif
            }
        }

        public static bool TrySubtract(GameObject target, GameObject cutter, out string error)
        {
            error = null;

            if (target == null || cutter == null)
            {
                error = "Boolean target or cutter is null.";
                return false;
            }

#if PROBUILDER_EXPERIMENTAL_FEATURES
            MeshFilter filter = target.GetComponent<MeshFilter>();
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            MeshFilter cutterFilter = cutter.GetComponent<MeshFilter>();

            if (filter == null || filter.sharedMesh == null)
            {
                error = "Boolean target needs a MeshFilter with a valid mesh.";
                return false;
            }

            if (cutterFilter == null || cutterFilter.sharedMesh == null)
            {
                error = "Cutter needs a MeshFilter with a closed mesh.";
                return false;
            }

            try
            {
                Model result = CSG.Subtract(target, cutter);
                if (result == null)
                {
                    error = "CSG returned no result.";
                    return false;
                }

                Mesh resultMesh = (Mesh)result;
                resultMesh.name = target.name + "_Boolean";
                filter.sharedMesh = resultMesh;

                if (renderer != null && result.materials != null)
                    renderer.sharedMaterials = result.materials.ToArray();

                MeshCollider collider = target.GetComponent<MeshCollider>();
                if (collider != null)
                    collider.sharedMesh = resultMesh;

                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return false;
            }
#else
            error = "ProBuilder experimental CSG is unavailable. Install ProBuilder, then enable Experimental Features in Edit > Preferences/Settings > ProBuilder.";
            return false;
#endif
        }
    }
}
