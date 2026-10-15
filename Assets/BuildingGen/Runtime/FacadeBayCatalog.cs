using System.Collections.Generic;
using UnityEngine;

namespace Emadgh.BuildingGen
{
    [CreateAssetMenu(menuName = "BuildingGen/Facade Bay Catalog", fileName = "FacadeBayCatalog")]
    public sealed class FacadeBayCatalog : ScriptableObject
    {
        [Tooltip("Prefabs should contain FacadeBayDefinition on their root.")]
        public List<GameObject> prefabs = new List<GameObject>();

        [Header("Blank Bays")]
        [Range(0f, 1f)] public float blankWeight = 0.12f;
        [Min(0.2f)] public float blankWidth = 1.2f;

        public bool HasUsablePrefabs
        {
            get
            {
                for (int i = 0; i < prefabs.Count; i++)
                {
                    GameObject prefab = prefabs[i];
                    if (prefab != null && prefab.GetComponent<FacadeBayDefinition>() != null)
                        return true;
                }

                return false;
            }
        }
    }
}
