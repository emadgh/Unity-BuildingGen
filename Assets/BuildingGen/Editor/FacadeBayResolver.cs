using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Emadgh.BuildingGen.Editor
{
    /// <summary>
    /// Lightweight v0.5 facade reservation resolver. It operates after generation and removes
    /// lower-priority generated modules whose occupied renderer bounds conflict with a higher-priority bay.
    /// This keeps the current BuildingBuilder stable while establishing a reusable bay-conflict layer.
    /// </summary>
    [InitializeOnLoad]
    internal static class FacadeBayResolver
    {
        private static bool queued;
        private static bool resolving;

        static FacadeBayResolver()
        {
            EditorApplication.hierarchyChanged += QueueResolve;
        }

        private static void QueueResolve()
        {
            if (resolving || queued || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            queued = true;
            EditorApplication.delayCall += ResolveAllGeneratedBuildings;
        }

        [MenuItem("Tools/BuildingGen/Resolve Generated Facade Conflicts")]
        public static void ResolveAllGeneratedBuildings()
        {
            if (resolving)
                return;

            queued = false;
            resolving = true;
            try
            {
                BuildingGenerator[] generators = Resources.FindObjectsOfTypeAll<BuildingGenerator>();
                for (int i = 0; i < generators.Length; i++)
                {
                    BuildingGenerator generator = generators[i];
                    if (generator == null || !generator.gameObject.scene.IsValid() || generator.generatedRoot == null)
                        continue;
                    Resolve(generator);
                }
            }
            finally
            {
                resolving = false;
            }
        }

        public static int Resolve(BuildingGenerator generator)
        {
            if (generator == null || generator.generatedRoot == null)
                return 0;

            YazdModuleDefinition[] modules = generator.generatedRoot.GetComponentsInChildren<YazdModuleDefinition>(true);
            List<YazdModuleDefinition> iwans = Collect(modules, YazdModuleKind.Iwan);
            List<YazdModuleDefinition> arcades = Collect(modules, YazdModuleKind.ArcadeBay);
            List<YazdModuleDefinition> badgirs = Collect(modules, YazdModuleKind.Badgir);
            List<YazdModuleDefinition> domes = Collect(modules, YazdModuleKind.Dome);
            List<YazdModuleDefinition> vaults = Collect(modules, YazdModuleKind.BarrelVault);

            int removed = 0;
            removed += RemoveLowerPriorityOverlaps(iwans, arcades, 0.08f);
            removed += RemoveLowerPriorityOverlaps(badgirs, domes, 0.10f);
            removed += RemoveLowerPriorityOverlaps(badgirs, vaults, 0.10f);

            if (removed > 0)
                EditorUtility.SetDirty(generator);
            return removed;
        }

        private static List<YazdModuleDefinition> Collect(YazdModuleDefinition[] source, YazdModuleKind kind)
        {
            List<YazdModuleDefinition> result = new List<YazdModuleDefinition>();
            for (int i = 0; i < source.Length; i++)
            {
                YazdModuleDefinition module = source[i];
                if (module != null && module.kind == kind)
                    result.Add(module);
            }
            return result;
        }

        private static int RemoveLowerPriorityOverlaps(
            List<YazdModuleDefinition> highPriority,
            List<YazdModuleDefinition> lowPriority,
            float padding)
        {
            int removed = 0;
            for (int lowIndex = lowPriority.Count - 1; lowIndex >= 0; lowIndex--)
            {
                YazdModuleDefinition low = lowPriority[lowIndex];
                if (low == null)
                    continue;

                Bounds lowBounds;
                if (!TryGetRendererBounds(low.gameObject, out lowBounds))
                    continue;

                bool overlaps = false;
                for (int highIndex = 0; highIndex < highPriority.Count; highIndex++)
                {
                    YazdModuleDefinition high = highPriority[highIndex];
                    if (high == null)
                        continue;

                    Bounds highBounds;
                    if (!TryGetRendererBounds(high.gameObject, out highBounds))
                        continue;

                    if (OverlapsXZ(highBounds, lowBounds, padding))
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                    continue;

                Object.DestroyImmediate(low.gameObject);
                removed++;
            }
            return removed;
        }

        private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            bounds = new Bounds(root.transform.position, Vector3.zero);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private static bool OverlapsXZ(Bounds a, Bounds b, float padding)
        {
            float aMinX = a.min.x - padding;
            float aMaxX = a.max.x + padding;
            float aMinZ = a.min.z - padding;
            float aMaxZ = a.max.z + padding;

            float bMinX = b.min.x - padding;
            float bMaxX = b.max.x + padding;
            float bMinZ = b.min.z - padding;
            float bMaxZ = b.max.z + padding;

            return aMinX <= bMaxX && aMaxX >= bMinX &&
                   aMinZ <= bMaxZ && aMaxZ >= bMinZ;
        }
    }
}
