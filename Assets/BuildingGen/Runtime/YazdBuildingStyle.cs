using System.Collections.Generic;
using UnityEngine;

namespace Emadgh.BuildingGen
{
    [CreateAssetMenu(menuName = "BuildingGen/Yazd Building Style", fileName = "YazdBuildingStyle")]
    public sealed class YazdBuildingStyle : ScriptableObject
    {
        [Header("Surface")]
        public Material wallMaterial;
        public Material roofMaterial;

        [Header("Openings")]
        public List<GameObject> doorPrefabs = new List<GameObject>();
        public List<GameObject> windowPrefabs = new List<GameObject>();

        [Header("Yazd Massing")]
        [Range(0f, 1f)] public float frontWindowChance = 1f;
        [Range(0f, 1f)] public float backWindowChance = 0.45f;
        [Range(0f, 1f)] public float sideWindowChance = 0.2f;
        [Range(0f, 1f)] public float centeredDoorChance = 0.65f;

        [Header("Roof / Parapet")]
        public bool generateFlatRoof = true;
        public bool generateParapet = true;
        [Min(0.05f)] public float roofThickness = 0.22f;
        [Min(0.05f)] public float parapetHeight = 0.55f;
        [Min(0.05f)] public float parapetThickness = 0.22f;
    }
}
