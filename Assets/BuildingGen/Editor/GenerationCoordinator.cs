using UnityEngine;
using Object = UnityEngine.Object;

namespace Emadgh.BuildingGen.Editor
{
    /// <summary>
    /// Selects the active geometry backend.
    /// SolidMesh is the production direction; LegacySegmented is retained temporarily for comparison/fallback.
    /// </summary>
    internal static class GenerationCoordinator
    {
        public static void Generate(BuildingGenerator generator)
        {
            if (generator == null)
                return;

            if (generator.footprint == BuildingFootprint.CustomSpline)
            {
                if (generator.geometryMode != BuildingGeometryMode.SolidMesh)
                {
                    Debug.LogWarning(
                        "BuildingGen: CustomSpline is a solid-mesh footprint and ignores LegacySegmented mode.",
                        generator);
                }

                if (!CustomFootprintValidationUtility.ValidateForBuild(
                        generator,
                        out string validationError))
                {
                    Debug.LogError(
                        "BuildingGen custom footprint: " + validationError,
                        generator);
                    return;
                }

                CustomSplineSolidBuildingBuilder.Generate(generator);
                return;
            }

            if (generator.geometryMode == BuildingGeometryMode.SolidMesh)
            {
                SolidBuildingBuilder.Generate(generator);
                return;
            }

            GenerateLegacy(generator);
        }

        public static void Clear(BuildingGenerator generator)
        {
            if (generator == null)
                return;

            bool hasSolidBody =
                generator.generatedRoot != null
                && generator.generatedRoot.Find("SolidBuildingMesh") != null;

            if (generator.geometryMode == BuildingGeometryMode.SolidMesh
                || generator.footprint == BuildingFootprint.CustomSpline
                || hasSolidBody)
            {
                SolidBuildingBuilder.Clear(generator);
            }
            else
            {
                BuildingBuilder.Clear(generator);
            }
        }

        public static GameObject BakeCopy(BuildingGenerator generator)
        {
            if (generator == null)
                return null;

            bool hasSolidBody =
                generator.generatedRoot != null
                && generator.generatedRoot.Find("SolidBuildingMesh") != null;

            if (generator.geometryMode == BuildingGeometryMode.SolidMesh
                || generator.footprint == BuildingFootprint.CustomSpline
                || hasSolidBody)
            {
                return SolidBuildingBuilder.BakeCopy(generator);
            }

            GameObject baked = BuildingBuilder.BakeCopy(generator);
            if (baked == null)
                return null;

            FacadeBayDefinition[] definitions =
                baked.GetComponentsInChildren<FacadeBayDefinition>(true);
            for (int i = 0; i < definitions.Length; i++)
            {
                FacadeBayDefinition definition = definitions[i];
                if (definition.cutterRoot != null)
                    Object.DestroyImmediate(definition.cutterRoot.gameObject);
                Object.DestroyImmediate(definition);
            }

            return baked;
        }

        private static void GenerateLegacy(BuildingGenerator generator)
        {
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
                generator.windowsPerFloor = Vector2Int.zero;
                generator.generateFrontOpenings =
                    frontOpenings
                    && generator.keepPrimaryEntranceDoor
                    && generator.generateGroundFloorDoor;
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
    }
}
