using UnityEngine;

namespace Emadgh.BuildingGen
{
    public enum OpeningKind
    {
        Door,
        Window,
        Arch,
        Balcony
    }

    /// <summary>
    /// Put this component on the root of every door/window prefab.
    /// The prefab contains the visible art plus a closed cutter mesh used for editor-time CSG.
    /// Convention: prefab pivot is bottom-center of the opening, local +Z points out of the facade.
    /// </summary>
    public sealed class OpeningDefinition : MonoBehaviour
    {
        [Header("Classification")]
        public OpeningKind kind = OpeningKind.Window;
        [Min(0.01f)] public float selectionWeight = 1f;

        [Header("Prefab Parts")]
        [Tooltip("Visible imported art. This remains after generation.")]
        public Transform visualRoot;
        [Tooltip("Closed/manifold mesh used only for subtraction. It should extend through the full wall thickness.")]
        public Transform cutterRoot;

        [Header("Placement Envelope")]
        [Min(0.1f)] public float nominalWidth = 1.2f;
        [Min(0.1f)] public float nominalHeight = 1.8f;
        [Min(0f)] public float sillHeight = 0.9f;
        [Min(0f)] public float edgeClearance = 0.45f;
        [Min(0f)] public float neighborClearance = 0.35f;

        [Header("Floor Rules")]
        public bool allowGroundFloor = true;
        public bool allowUpperFloors = true;

        public bool IsAllowedOnFloor(int floorIndex)
        {
            return floorIndex == 0 ? allowGroundFloor : allowUpperFloors;
        }

        public GameObject CutterObject
        {
            get { return cutterRoot != null ? cutterRoot.gameObject : null; }
        }
    }
}
