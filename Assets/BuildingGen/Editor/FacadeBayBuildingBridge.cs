using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emadgh.BuildingGen.Editor
{
    internal static class FacadeBayBuildingBridge
    {
        private const string GeneratedRootName = "FacadeBays";

        public static void Generate(BuildingGenerator generator)
        {
            if (generator == null || generator.generatedRoot == null)
                return;

            FacadeBayCatalog catalog = generator.facadeBayCatalog;
            if (catalog == null)
            {
                Debug.LogWarning("BuildingGen v0.5: Facade Bay Planner is enabled but no FacadeBayCatalog is assigned.", generator);
                return;
            }

            Transform structureRoot = generator.generatedRoot.Find("Structure");
            if (structureRoot == null)
            {
                Debug.LogWarning("BuildingGen v0.5: generated Structure root was not found.", generator);
                return;
            }

            Transform oldRoot = generator.generatedRoot.Find(GeneratedRootName);
            if (oldRoot != null)
                Undo.DestroyObjectImmediate(oldRoot.gameObject);

            GameObject bayRootObject = new GameObject(GeneratedRootName);
            Undo.RegisterCreatedObjectUndo(bayRootObject, "Generate facade bays");
            bayRootObject.transform.SetParent(generator.generatedRoot, false);
            Transform bayRoot = bayRootObject.transform;

            System.Random random = new System.Random(unchecked(generator.seed * 486187739 + 104729));
            bool warnedAboutBoolean = false;

            for (int i = 0; i < structureRoot.childCount; i++)
            {
                Transform child = structureRoot.GetChild(i);
                GameObject wall = child.gameObject;
                if (wall.GetComponent<MeshFilter>() == null)
                    continue;

                FacadeSurfaceMask surface = ClassifySurface(generator, wall.transform);
                if ((generator.facadeBaySurfaces & surface) == 0)
                    continue;

                GenerateForWall(generator, catalog, random, wall, surface, bayRoot, ref warnedAboutBoolean);
                RefreshCollider(wall);
            }
        }

        private static void GenerateForWall(
            BuildingGenerator generator,
            FacadeBayCatalog catalog,
            System.Random random,
            GameObject wall,
            FacadeSurfaceMask surface,
            Transform bayRoot,
            ref bool warnedAboutBoolean)
        {
            float wallWidth = wall.transform.TransformVector(Vector3.right).magnitude;
            float wallHeight = wall.transform.TransformVector(Vector3.up).magnitude;
            float wallThickness = wall.transform.TransformVector(Vector3.forward).magnitude;
            if (wallWidth < 0.2f || wallHeight < 0.2f)
                return;

            FacadeBayReservationMap reservations = new FacadeBayReservationMap();
            ReserveExistingOpenings(generator, wall, wallWidth, wallHeight, wallThickness, reservations);

            int specialCount = reservations.CountKind(FacadeBayKind.Door)
                               + reservations.CountKind(FacadeBayKind.Iwan)
                               + reservations.CountKind(FacadeBayKind.Arcade);

            int minCount = Mathf.Max(0, generator.facadeBaysPerFloor.x);
            int maxCount = Mathf.Max(minCount, generator.facadeBaysPerFloor.y);

            for (int floor = 0; floor < generator.floors; floor++)
            {
                int count = random.Next(minCount, maxCount + 1);
                if (count <= 0)
                    continue;

                float step = wallWidth / (count + 1f);
                for (int index = 0; index < count; index++)
                {
                    float x = -wallWidth * 0.5f + step * (index + 1);
                    float jitter = step * generator.facadeBayHorizontalJitter;
                    x += Range(random, -jitter, jitter);

                    if (Next01(random) > generator.facadeBayFillChance)
                    {
                        ReserveBlank(catalog, reservations, floor, x, wallWidth);
                        continue;
                    }

                    GameObject prefab;
                    FacadeBayDefinition sourceDefinition;
                    bool blank;
                    PickBay(
                        catalog,
                        generator,
                        random,
                        floor,
                        surface,
                        specialCount,
                        out prefab,
                        out sourceDefinition,
                        out blank);

                    if (blank)
                    {
                        ReserveBlank(catalog, reservations, floor, x, wallWidth);
                        continue;
                    }

                    if (prefab == null || sourceDefinition == null)
                        continue;

                    float width = sourceDefinition.nominalWidth;
                    float half = width * 0.5f;
                    float edge = Mathf.Max(0f, sourceDefinition.edgeClearance);
                    float minX = -wallWidth * 0.5f + edge + half;
                    float maxX = wallWidth * 0.5f - edge - half;
                    if (minX > maxX)
                        continue;

                    x = Mathf.Clamp(x, minX, maxX);
                    if (!reservations.TryReserve(
                            floor,
                            sourceDefinition.floorSpan,
                            x,
                            width,
                            sourceDefinition.neighborClearance,
                            sourceDefinition.kind))
                    {
                        continue;
                    }

                    if (IsSpecial(sourceDefinition.kind))
                        specialCount++;

                    PlaceBay(
                        generator,
                        wall,
                        wallHeight,
                        prefab,
                        sourceDefinition,
                        floor,
                        x,
                        bayRoot,
                        index,
                        ref warnedAboutBoolean);
                }
            }
        }

        private static void ReserveExistingOpenings(
            BuildingGenerator generator,
            GameObject wall,
            float wallWidth,
            float wallHeight,
            float wallThickness,
            FacadeBayReservationMap reservations)
        {
            OpeningDefinition[] existing = generator.generatedRoot.GetComponentsInChildren<OpeningDefinition>(true);
            Vector3 right = wall.transform.right.normalized;
            Vector3 up = wall.transform.up.normalized;
            Vector3 forward = wall.transform.forward.normalized;
            float floorHeight = Mathf.Max(0.1f, generator.floorHeight * generator.transform.TransformVector(Vector3.up).magnitude);

            for (int i = 0; i < existing.Length; i++)
            {
                OpeningDefinition definition = existing[i];
                if (definition == null)
                    continue;

                Vector3 delta = definition.transform.position - wall.transform.position;
                float depth = Mathf.Abs(Vector3.Dot(delta, forward));
                if (depth > Mathf.Max(0.75f, wallThickness * 2.5f))
                    continue;

                float x = Vector3.Dot(delta, right);
                if (Mathf.Abs(x) > wallWidth * 0.65f)
                    continue;

                float baseY = Vector3.Dot(delta, up) + wallHeight * 0.5f;
                int floor = Mathf.Clamp(Mathf.FloorToInt((baseY + 0.01f) / floorHeight), 0, Mathf.Max(0, generator.floors - 1));
                FacadeBayKind kind = definition.kind == OpeningKind.Door ? FacadeBayKind.Door : FacadeBayKind.Window;

                reservations.TryReserve(
                    floor,
                    1,
                    x,
                    definition.nominalWidth,
                    definition.neighborClearance,
                    kind);
            }
        }

        private static void ReserveBlank(
            FacadeBayCatalog catalog,
            FacadeBayReservationMap reservations,
            int floor,
            float x,
            float wallWidth)
        {
            float width = Mathf.Min(Mathf.Max(0.2f, catalog.blankWidth), wallWidth);
            float half = width * 0.5f;
            x = Mathf.Clamp(x, -wallWidth * 0.5f + half, wallWidth * 0.5f - half);
            reservations.TryReserve(floor, 1, x, width, 0.05f, FacadeBayKind.Blank);
        }

        private static void PickBay(
            FacadeBayCatalog catalog,
            BuildingGenerator generator,
            System.Random random,
            int floor,
            FacadeSurfaceMask surface,
            int specialCount,
            out GameObject selectedPrefab,
            out FacadeBayDefinition selectedDefinition,
            out bool blank)
        {
            selectedPrefab = null;
            selectedDefinition = null;
            blank = false;

            float totalWeight = Mathf.Max(0f, catalog.blankWeight);
            List<GameObject> prefabs = new List<GameObject>();
            List<FacadeBayDefinition> definitions = new List<FacadeBayDefinition>();
            List<float> weights = new List<float>();

            for (int i = 0; i < catalog.prefabs.Count; i++)
            {
                GameObject prefab = catalog.prefabs[i];
                if (prefab == null)
                    continue;

                FacadeBayDefinition definition = prefab.GetComponent<FacadeBayDefinition>();
                if (definition == null || !definition.IsAllowed(floor, generator.floors, surface))
                    continue;

                if (IsSpecial(definition.kind) && specialCount >= generator.maxSpecialBaysPerWall)
                    continue;

                float weight = Mathf.Max(0.001f, definition.selectionWeight);
                prefabs.Add(prefab);
                definitions.Add(definition);
                weights.Add(weight);
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
                return;

            float pick = Range(random, 0f, totalWeight);
            float cursor = Mathf.Max(0f, catalog.blankWeight);
            if (pick <= cursor)
            {
                blank = true;
                return;
            }

            for (int i = 0; i < prefabs.Count; i++)
            {
                cursor += weights[i];
                if (pick <= cursor)
                {
                    selectedPrefab = prefabs[i];
                    selectedDefinition = definitions[i];
                    return;
                }
            }

            if (prefabs.Count > 0)
            {
                selectedPrefab = prefabs[prefabs.Count - 1];
                selectedDefinition = definitions[definitions.Count - 1];
            }
        }

        private static void PlaceBay(
            BuildingGenerator generator,
            GameObject wall,
            float wallHeight,
            GameObject prefab,
            FacadeBayDefinition sourceDefinition,
            int floor,
            float horizontal,
            Transform parent,
            int index,
            ref bool warnedAboutBoolean)
        {
            GameObject instance;
            if (PrefabUtility.IsPartOfPrefabAsset(prefab))
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            else
                instance = Object.Instantiate(prefab, parent);

            instance.name = prefab.name + "_F" + floor + "_" + index.ToString("00");

            float baseY = floor * generator.floorHeight + sourceDefinition.sillHeight;
            Vector3 position = wall.transform.position
                             + wall.transform.right.normalized * horizontal
                             + wall.transform.up.normalized * (baseY - wallHeight * 0.5f);
            instance.transform.position = position;
            instance.transform.rotation = wall.transform.rotation;

            FacadeBayDefinition definition = instance.GetComponent<FacadeBayDefinition>();
            if (definition == null)
                definition = sourceDefinition;

            if (definition.visualRoot != null && Mathf.Abs(definition.visualDepthOffset) > 0.0001f)
                definition.visualRoot.localPosition += Vector3.forward * definition.visualDepthOffset;

            if (!definition.cutHostSurface)
                return;

            GameObject cutter = definition.CutterObject;
            if (cutter == null)
            {
                Debug.LogWarning("BuildingGen v0.5: facade bay prefab '" + prefab.name + "' requests a host cut but has no Cutter Root.", instance);
                return;
            }

            string error;
            if (!ProBuilderBooleanBackend.TrySubtract(wall, cutter, out error) && !warnedAboutBoolean)
            {
                Debug.LogWarning("BuildingGen v0.5: facade bay CSG is unavailable or failed. " + error, generator);
                warnedAboutBoolean = true;
            }

            cutter.SetActive(false);
        }

        private static FacadeSurfaceMask ClassifySurface(BuildingGenerator generator, Transform wall)
        {
            if (wall.name.StartsWith("Courtyard_"))
                return FacadeSurfaceMask.Courtyard;

            Vector3 forward = generator.transform.InverseTransformDirection(wall.forward).normalized;
            if (forward.z > 0.55f)
                return FacadeSurfaceMask.Front;
            if (forward.z < -0.55f)
                return FacadeSurfaceMask.Back;
            return FacadeSurfaceMask.Side;
        }

        private static bool IsSpecial(FacadeBayKind kind)
        {
            return kind == FacadeBayKind.Door || kind == FacadeBayKind.Iwan || kind == FacadeBayKind.Arcade;
        }

        private static void RefreshCollider(GameObject wall)
        {
            MeshFilter filter = wall.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return;

            MeshCollider collider = wall.GetComponent<MeshCollider>();
            if (collider == null)
                collider = wall.AddComponent<MeshCollider>();

            collider.sharedMesh = null;
            collider.sharedMesh = filter.sharedMesh;
        }

        private static float Next01(System.Random random)
        {
            return (float)random.NextDouble();
        }

        private static float Range(System.Random random, float min, float max)
        {
            return min + (max - min) * Next01(random);
        }
    }
}
