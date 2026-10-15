using UnityEngine;

namespace Emadgh.BuildingGen
{
    public enum YazdModuleKind
    {
        Iwan,
        Badgir,
        RoofProp,
        FacadeProp
    }

    /// <summary>
    /// Optional metadata for reusable architectural prefabs such as iwans and badgirs.
    /// The prefab pivot should be bottom-center, with +Z pointing outward from the facade.
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
    }
}
