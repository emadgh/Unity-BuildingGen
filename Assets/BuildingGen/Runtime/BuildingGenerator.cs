using UnityEngine;

namespace Emadgh.BuildingGen
{
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
        [Min(2f)] public float width = 8f;
        [Min(2f)] public float depth = 6f;
        [Range(1, 5)] public int floors = 2;
        [Min(2f)] public float floorHeight = 3.1f;
        [Min(0.12f)] public float wallThickness = 0.35f;

        [Header("Openings")]
        public Vector2Int windowsPerFloor = new Vector2Int(2, 4);
        public bool generateFrontOpenings = true;
        public bool generateBackOpenings = true;
        public bool generateSideOpenings = false;
        public bool generateGroundFloorDoor = true;

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
    }
}
