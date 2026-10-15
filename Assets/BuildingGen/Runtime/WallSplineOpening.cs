using System;
using UnityEngine;

namespace Emadgh.BuildingGen
{
    [Serializable]
    public sealed class WallSplineOpening
    {
        public bool enabled = true;
        [Tooltip("Distance in meters from the start of the sampled wall path.")]
        [Min(0f)] public float distance = 3f;
        [Min(0.3f)] public float width = 1.8f;
        [Tooltip("Clear opening height. If it is lower than the wall, the generator keeps a lintel/wall strip above the opening.")]
        [Min(0.3f)] public float clearHeight = 2.1f;

        [Header("Optional Gate / Arch Prefab")]
        [Tooltip("Bottom-center pivot. Local +X runs along the wall and local +Z faces outward from the right side of the spline direction.")]
        public GameObject framePrefab;
        public float verticalOffset;
        [Min(0.01f)] public float uniformScale = 1f;

        [Header("Opening Edge Pillars")]
        public bool generateEdgePillars = true;
        [Min(0.08f)] public float pillarWidth = 0.28f;
        [Min(0f)] public float pillarExtraDepth = 0.15f;
    }
}
