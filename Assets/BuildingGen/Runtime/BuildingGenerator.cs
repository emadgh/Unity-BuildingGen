using UnityEngine;

namespace Emadgh.BuildingGen
{
    public enum BuildingFootprint
    {
        Rectangle,
        LShape,
        UShape,
        Courtyard
    }

    /// <summary>
    /// Scene component that stores deterministic building-generation settings.
    /// Geometry is generated from the custom inspector in edit mode.
    /// </summary>
    public sealed class BuildingGenerator : MonoBehaviour
    {
        [Header("Style")]
        public YazdBuildingStyle style;

        [Header("Seed")]
        public int seed = 1001;

        [Header("Footprint")]
        public BuildingFootprint footprint = BuildingFootprint.Rectangle;
        [Min(4f)] public float width = 10f;
        [Min(4f)] public float depth = 8f;
        [Range(1, 5)] public int floors = 2;
        [Min(2f)] public float floorHeight = 3.1f;
        [Min(0.12f)] public float wallThickness = 0.35f;

        [Header("L / U Shape")]
        [Tooltip("Width of the solid side/rear wings used by L and U footprints.")]
        [Min(1.5f)] public float wingWidth = 3.2f;
        [Tooltip("How far the U-shaped courtyard/recess extends into the building from the front.")]
        [Min(1.5f)] public float uRecessDepth = 4.5f;

        [Header("Central Courtyard")]
        [Min(2f)] public float courtyardWidth = 4.5f;
        [Min(2f)] public float courtyardDepth = 3.8f;
        public bool generateCourtyardOpenings = true;

        [Header("Openings")]
        public Vector2Int windowsPerFloor = new Vector2Int(2, 4);
        public bool generateFrontOpenings = true;
        public bool generateBackOpenings = true;
        public bool generateSideOpenings = false;
        public bool generateGroundFloorDoor = true;
        public bool recessedEntrance = true;
        [Range(0f, 1.2f)] public float entranceRecess = 0.35f;

        [Header("Yazd Modules")]
        public bool generateIwan = true;
        public bool generateBadgir = true;
        [Range(0, 3)] public int badgirCount = 1;

        [Header("Variation")]
        [Range(0f, 0.35f)] public float horizontalJitter = 0.08f;
        [Range(0f, 0.25f)] public float sillJitter = 0.04f;

        [HideInInspector] public Transform generatedRoot;

        public float TotalHeight
        {
            get { return floors * floorHeight; }
        }

        public void IncrementSeed()
        {
            unchecked { seed++; }
        }

        public void DecrementSeed()
        {
            unchecked { seed--; }
        }

        private void OnValidate()
        {
            width = Mathf.Max(4f, width);
            depth = Mathf.Max(4f, depth);
            wallThickness = Mathf.Min(wallThickness, Mathf.Min(width, depth) * 0.2f);
            wingWidth = Mathf.Clamp(wingWidth, 1.5f, Mathf.Min(width, depth) * 0.45f);
            uRecessDepth = Mathf.Clamp(uRecessDepth, 1.5f, depth - wallThickness * 4f);

            float maxCourtyardWidth = Mathf.Max(2f, width - wallThickness * 6f);
            float maxCourtyardDepth = Mathf.Max(2f, depth - wallThickness * 6f);
            courtyardWidth = Mathf.Clamp(courtyardWidth, 2f, maxCourtyardWidth);
            courtyardDepth = Mathf.Clamp(courtyardDepth, 2f, maxCourtyardDepth);
        }
    }
}
