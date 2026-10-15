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

    public enum WallButtressSide
    {
        Right,
        Left,
        Both
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

        [Header("Broken Top Profile")]
        public bool generateBrokenTopProfile;
        [Range(0f, 1f)] public float brokenTopChance = 0.35f;
        public Vector2 brokenTopDropRange = new Vector2(0.08f, 0.5f);

        [Header("Buttresses")]
        public bool generateButtresses;
        [Min(0.4f)] public float buttressSpacing = 3.2f;
        [Range(0f, 1f)] public float buttressChance = 0.85f;
        [Min(0.08f)] public float buttressWidth = 0.35f;
        [Min(0f)] public float buttressExtraDepth = 0.28f;
        [Range(0.1f, 1.2f)] public float buttressHeightScale = 0.78f;
        public WallButtressSide buttressSide = WallButtressSide.Right;

        [Header("Open Wall End Caps")]
        public bool generateEndCaps = true;
        [Min(0.08f)] public float endCapWidth = 0.46f;
        [Min(0f)] public float endCapExtraDepth = 0.18f;

        [Header("Top / Crown Prefabs")]
        public bool generateTopPrefabs = false;
        public List<WallTopPrefabOption> topPrefabs = new List<WallTopPrefabOption>();
        [Min(0.2f)] public float topSpacing = 1.8f;
        [Range(0f, 1f)] public float topSpawnChance = 0.9f;
        public float topVerticalOffset = 0f;
        [Range(0f, 0.5f)] public float topLateralJitter = 0.05f;
        [Range(0f, 30f)] public float topYawJitter = 4f;
        public Vector2 topScaleRange = new Vector2(0.95f, 1.05f);

        private void OnValidate()
        {
            wallHeight = Mathf.Max(0.25f, wallHeight);
            wallThickness = Mathf.Max(0.08f, wallThickness);
            chunkLength = Mathf.Max(0.25f, chunkLength);

            brokenTopDropRange.x = Mathf.Max(0f, brokenTopDropRange.x);
            brokenTopDropRange.y = Mathf.Max(brokenTopDropRange.x, brokenTopDropRange.y);

            buttressSpacing = Mathf.Max(0.4f, buttressSpacing);
            buttressWidth = Mathf.Max(0.08f, buttressWidth);
            endCapWidth = Mathf.Max(0.08f, endCapWidth);

            topSpacing = Mathf.Max(0.2f, topSpacing);
            topScaleRange.x = Mathf.Max(0.05f, topScaleRange.x);
            topScaleRange.y = Mathf.Max(topScaleRange.x, topScaleRange.y);
        }
    }
}
