using UnityEngine;
using Object = UnityEngine.Object;

namespace Emadgh.BuildingGen.Editor
{
    /// <summary>
    /// Keeps the original generator intact while allowing v0.5 facade bays to replace
    /// legacy random facade openings. This makes the new planner opt-in and reversible.
    /// </summary>
    internal static class GenerationCoordinator
    {
        public static void Generate(BuildingGenerator generator)
        {
            if (generator == null)
                return;

            if (!generator.useFacadeBayPlanner)
            {
                BuildingBuilder.Generate(generator);
                return;
            }

            Vector2Int windowsPerFloor = generator.windowsPerFloor;
            bool frontOpenings = generator.generateFrontOpenings;
            bool backOpenings = generator.generateBackOpenings;
            bool sideOpenings = generator.generateSideOpenings;
            bool courtyardOpenings = generator.generateCourtyardOpenings;
            bool generateIwan = generator.generateIwan;
            bool generateCourtyardArcade = generator.generateCourtyardArcade;

            try
            {
                // Keep only the legacy primary entrance when requested. Its position is reserved
                // by FacadeBayBuildingBridge before the new bays are planned, so hashti still works.
                generator.windowsPerFloor = Vector2Int.zero;
                generator.generateFrontOpenings = frontOpenings && generator.keepPrimaryEntranceDoor && generator.generateGroundFloorDoor;
                generator.generateBackOpenings = false;
                generator.generateSideOpenings = false;
                generator.generateCourtyardOpenings = false;
                generator.generateIwan = false;
                generator.generateCourtyardArcade = false;

                BuildingBuilder.Generate(generator);
            }
            finally
            {
                generator.windowsPerFloor = windowsPerFloor;
                generator.generateFrontOpenings = frontOpenings;
                generator.generateBackOpenings = backOpenings;
                generator.generateSideOpenings = sideOpenings;
                generator.generateCourtyardOpenings = courtyardOpenings;
                generator.generateIwan = generateIwan;
                generator.generateCourtyardArcade = generateCourtyardArcade;
            }

            FacadeBayBuildingBridge.Generate(generator);
        }

        public static void Clear(BuildingGenerator generator)
        {
            BuildingBuilder.Clear(generator);
        }

        public static GameObject BakeCopy(BuildingGenerator generator)
        {
            GameObject baked = BuildingBuilder.BakeCopy(generator);
            if (baked == null)
                return null;

            FacadeBayDefinition[] definitions = baked.GetComponentsInChildren<FacadeBayDefinition>(true);
            for (int i = 0; i < definitions.Length; i++)
            {
                FacadeBayDefinition definition = definitions[i];
                if (definition.cutterRoot != null)
                    Object.DestroyImmediate(definition.cutterRoot.gameObject);
                Object.DestroyImmediate(definition);
            }

            return baked;
        }
    }
}
