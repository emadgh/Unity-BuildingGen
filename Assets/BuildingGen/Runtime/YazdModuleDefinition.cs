using UnityEngine;

namespace Emadgh.BuildingGen
{
    public enum YazdModuleKind
    {
        Iwan,
        Badgir,
        ArcadeBay,
        Dome,
        BarrelVault,
        Hashti,
        RoofProp,
        FacadeProp
    }

    /// <summary>
    /// Metadata for reusable architectural prefabs such as iwans, badgirs,
    /// arcade bays, domes and barrel vaults.
    /// Facade-module pivot convention: bottom-center, +Y up, +Z outward.
    /// Roof modules use bottom-center as well, with +Y up.
    /// </summary>
    public sealed class YazdModuleDefinition : MonoBehaviour
    {
        public YazdModuleKind kind = YazdModuleKind.FacadeProp;
        [Min(0.01f)] public float selectionWeight = 1f;
        [Min(0.1f)] public float nominalWidth = 2f;
        [Min(0.1f)] public float nominalDepth = 1f;
        [Min(0.1f)] public float nominalHeight = 3f;
        [Tooltip("Extra distance from footprint edges when placing this module.")]
        [Min(0f)] public float edgeClearance = 0.35f;

        [Header("Optional CSG")]
        [Tooltip("Visible art root. Optional, but useful when the art needs an inset independent of the cutter.")]
        public Transform visualRoot;
        [Tooltip("Closed/manifold cutter mesh used for an editor-time subtraction from a host wall.")]
        public Transform cutterRoot;
        [Tooltip("Allow the generator to subtract Cutter Root from the host wall when this module is facade-mounted.")]
        public bool cutHostSurface;

        public GameObject CutterObject
        {
            get { return cutterRoot != null ? cutterRoot.gameObject : null; }
        }
    }
}
