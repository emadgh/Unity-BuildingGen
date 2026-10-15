using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emadgh.BuildingGen
{
    public enum WallSplineConstruction
    {
        SimpleMud,
        RuinedMud,
        DecoratedMud
    }

    [Serializable]
    public sealed class WallTopPrefabOption
    {
        public GameObject prefab;
        [Min(0.001f)] public float weight = 1f;
    }

    [CreateAssetMenu(menuName = "BuildingGen/Wall Spline Style", fileName = "WallSplineStyle")]
    public sealed class WallSplineStyle : ScriptableObject
    {
        [Header("Base Wall")]
        public WallSplineConstruction construction = WallSplineConstruction.SimpleMud;
        public Material wallMaterial;
        [Min(0.25f)] public float wallHeight = 2.4f;
        [Min(0.08f)] public float wallThickness = 0.42f;
        [Min(0.25f)] public float chunkLength = 1.2f;

        [Header("Ruined Variation")]
        [Range(0f, 1.5f)] public float heightVariation = 0.45f;
        [Range(0f, 0.25f)] public float thicknessVariation = 0.06f;
        [Range(0f, 0.65f)] public float missingChunkChance = 0.08f;
        [Range(0f, 0.35f)] public float baseVerticalJitter = 0.03f;
        [Range(0f, 12f)] public float yawJitter = 2.0f;

        [Header("Top / Crown Prefabs")]
        public bool generateTopPrefabs = false;
        public List<WallTopPrefabOption> topPrefabs = new List<WallTopPrefabOption>();
        [Min(0.2f)] public float topSpacing = 1.8f;
        [Range(0f, 1f)] public float topSpawnChance = 0.9f;
        public float topVerticalOffset = 0f;
        [Range(0f, 0.5f)] public float topLateralJitter = 0.05f;
        [Range(0f, 30f)] public float topYawJitter = 4f;
        public Vector2 topScaleRange = new Vector2(0.95f, 1.05f);

        [Header("Pillars")]
        public bool generatePillars = false;
        public List<WallTopPrefabOption> pillarPrefabs = new List<WallTopPrefabOption>();
        [Min(0.5f)] public float pillarSpacing = 3.2f;
        [Range(0f, 1f)] public float pillarSpawnChance = 1f;
        public float pillarVerticalOffset = 0f;
        public Vector2 pillarScaleRange = new Vector2(0.95f, 1.05f);

        [Header("Buttresses")]
        public bool generateButtresses = false;
        public List<WallTopPrefabOption> buttressPrefabs = new List<WallTopPrefabOption>();
        [Min(0.5f)] public float buttressSpacing = 4f;
        [Range(0f, 1f)] public float buttressSpawnChance = 0.7f;
        [Min(0f)] public float buttressOutwardOffset = 0.22f;
        public float buttressVerticalOffset = 0f;
        public Vector2 buttressScaleRange = new Vector2(0.9f, 1.1f);

        [Header("Niches / Wall Details")]
        public bool generateNiches = false;
        public List<WallTopPrefabOption> nichePrefabs = new List<WallTopPrefabOption>();
        [Min(0.5f)] public float nicheSpacing = 3.5f;
        [Range(0f, 1f)] public float nicheSpawnChance = 0.35f;
        public float nicheHeight = 1.05f;
        [Min(0f)] public float nicheSurfaceOffset = 0.02f;
        public Vector2 nicheScaleRange = new Vector2(0.9f, 1.08f);

        [Header("End Caps")]
        public bool generateEndCaps = false;
        public List<WallTopPrefabOption> endCapPrefabs = new List<WallTopPrefabOption>();
        public Vector2 endCapScaleRange = new Vector2(0.95f, 1.05f);

        private void OnValidate()
        {
            wallHeight = Mathf.Max(0.25f, wallHeight);
            wallThickness = Mathf.Max(0.08f, wallThickness);
            chunkLength = Mathf.Max(0.25f, chunkLength);

            topSpacing = Mathf.Max(0.2f, topSpacing);
            topScaleRange = ValidateScaleRange(topScaleRange);

            pillarSpacing = Mathf.Max(0.5f, pillarSpacing);
            pillarScaleRange = ValidateScaleRange(pillarScaleRange);

            buttressSpacing = Mathf.Max(0.5f, buttressSpacing);
            buttressOutwardOffset = Mathf.Max(0f, buttressOutwardOffset);
            buttressScaleRange = ValidateScaleRange(buttressScaleRange);

            nicheSpacing = Mathf.Max(0.5f, nicheSpacing);
            nicheSurfaceOffset = Mathf.Max(0f, nicheSurfaceOffset);
            nicheScaleRange = ValidateScaleRange(nicheScaleRange);

            endCapScaleRange = ValidateScaleRange(endCapScaleRange);
        }

        private static Vector2 ValidateScaleRange(Vector2 range)
        {
            range.x = Mathf.Max(0.05f, range.x);
            range.y = Mathf.Max(range.x, range.y);
            return range;
        }
    }
}
