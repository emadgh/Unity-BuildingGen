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

    public enum EntranceTurnDirection
    {
        Auto,
        Left,
        Right
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
        public bool generateCourtyardArcade = true;
        [Min(1.2f)] public float arcadeBayWidth = 2.2f;
        [Min(0.25f)] public float arcadeDepth = 0.8f;

        [Header("Openings")]
        public Vector2Int windowsPerFloor = new Vector2Int(2, 4);
        public bool generateFrontOpenings = true;
        public bool generateBackOpenings = true;
        public bool generateSideOpenings = false;
        public bool generateGroundFloorDoor = true;
        public bool recessedEntrance = true;
        [Range(0f, 1.2f)] public float entranceRecess = 0.35f;

        [Header("Entrance Sequence")]
        [Tooltip("Create a simple hashti + bent corridor guide behind the generated entrance. It is intentionally simple so production prefabs can replace it later.")]
        public bool generateHashtiAndCorridor = true;
        [Min(1.6f)] public float hashtiSize = 2.6f;
        [Min(1f)] public float entranceCorridorLength = 2.8f;
        [Min(0.8f)] public float entranceCorridorWidth = 1.25f;
        [Min(1f)] public float entranceTurnLength = 2.4f;
        public EntranceTurnDirection entranceTurn = EntranceTurnDirection.Auto;

        [Header("Yazd Modules")]
        public bool generateIwan = true;
        [Tooltip("If an iwan prefab contains a Cutter root, subtract it from the host wall before hiding the cutter.")]
        public bool cutIwanRecess = true;
        public bool generateBadgir = true;
        [Range(0, 3)] public int badgirCount = 1;

        [Header("Roof Forms")]
        public bool generateRoofForms = true;
        [Range(0, 4)] public int maxRoofForms = 2;
        [Range(0f, 1f)] public float roofFormChance = 0.55f;

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

            arcadeBayWidth = Mathf.Max(1.2f, arcadeBayWidth);
            arcadeDepth = Mathf.Max(0.25f, arcadeDepth);
            hashtiSize = Mathf.Max(1.6f, hashtiSize);
            entranceCorridorWidth = Mathf.Clamp(entranceCorridorWidth, 0.8f, hashtiSize * 0.8f);
            entranceCorridorLength = Mathf.Max(1f, entranceCorridorLength);
            entranceTurnLength = Mathf.Max(1f, entranceTurnLength);
        }
    }
}
