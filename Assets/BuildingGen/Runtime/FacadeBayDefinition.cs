using System;
using UnityEngine;

namespace Emadgh.BuildingGen
{
    public enum FacadeBayKind
    {
        Blank,
        Window,
        Door,
        Iwan,
        Arcade,
        Niche
    }

    [Flags]
    public enum FacadeSurfaceMask
    {
        None = 0,
        Front = 1 << 0,
        Back = 1 << 1,
        Side = 1 << 2,
        Courtyard = 1 << 3,
        All = Front | Back | Side | Courtyard
    }

    /// <summary>
    /// Unified metadata for production facade prefabs authored in 3ds Max or another DCC.
    /// Convention: root pivot is bottom-center, local +Y is up and local +Z points out of the facade.
    /// Cutter Root is optional and should be a closed mesh crossing the complete wall thickness.
    /// </summary>
    public sealed class FacadeBayDefinition : MonoBehaviour
    {
        [Header("Classification")]
        public FacadeBayKind kind = FacadeBayKind.Window;
        [Min(0.01f)] public float selectionWeight = 1f;

        [Header("Placement Envelope")]
        [Min(0.1f)] public float nominalWidth = 1.2f;
        [Min(0.1f)] public float nominalHeight = 1.8f;
        [Min(0f)] public float sillHeight = 0.9f;
        [Min(0f)] public float edgeClearance = 0.35f;
        [Min(0f)] public float neighborClearance = 0.25f;
        [Min(1)] public int floorSpan = 1;

        [Header("Placement Rules")]
        public bool allowGroundFloor = true;
        public bool allowUpperFloors = true;
        public FacadeSurfaceMask allowedSurfaces = FacadeSurfaceMask.All;

        [Header("Prefab Parts")]
        public Transform visualRoot;
        public Transform cutterRoot;
        public bool cutHostSurface = true;
        [Tooltip("Moves only Visual Root outward (+Z) after placement. Cutter Root remains centered on the host wall.")]
        public float visualDepthOffset;

        public bool IsAllowed(int floorIndex, int totalFloors, FacadeSurfaceMask surface)
        {
            if ((allowedSurfaces & surface) == 0)
                return false;

            if (floorIndex == 0)
            {
                if (!allowGroundFloor)
                    return false;
            }
            else if (!allowUpperFloors)
            {
                return false;
            }

            return floorIndex + Mathf.Max(1, floorSpan) <= Mathf.Max(1, totalFloors);
        }

        public GameObject CutterObject
        {
            get { return cutterRoot != null ? cutterRoot.gameObject : null; }
        }
    }
}
