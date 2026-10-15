using UnityEngine;

namespace Emadgh.BuildingGen
{
    public enum SolidFacadeRole
    {
        Front,
        Back,
        Side,
        Courtyard
    }

    /// <summary>
    /// Logical facade metadata for a procedurally generated solid building.
    /// This component has no renderer and does not contribute modular wall geometry.
    /// Its transform is centered on the facade, local +Z points outward and local +X runs along the facade.
    /// </summary>
    public sealed class SolidFacadeSurface : MonoBehaviour
    {
        public SolidFacadeRole role = SolidFacadeRole.Side;
        public bool courtyard;
        public float width = 1f;
        public float height = 3f;
        public float thickness = 0.35f;

        [HideInInspector] public GameObject solidBody;

        public FacadeSurfaceMask SurfaceMask
        {
            get
            {
                switch (role)
                {
                    case SolidFacadeRole.Front: return FacadeSurfaceMask.Front;
                    case SolidFacadeRole.Back: return FacadeSurfaceMask.Back;
                    case SolidFacadeRole.Courtyard: return FacadeSurfaceMask.Courtyard;
                    default: return FacadeSurfaceMask.Side;
                }
            }
        }
    }
}
