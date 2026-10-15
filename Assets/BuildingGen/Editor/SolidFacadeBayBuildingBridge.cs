using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emadgh.BuildingGen.Editor
{
    /// <summary>
    /// Facade-bay planner adapter for the v0.6 one-mesh building backend.
    /// It reads logical SolidFacadeSurface anchors but always applies CSG to the single SolidBuildingMesh.
    /// </summary>
    internal static class SolidFacadeBayBuildingBridge
    {
        private const string GeneratedRootName = "FacadeBays";

        public static void Generate(
            BuildingGenerator generator,
            GameObject body,
            List<SolidFacadeSurface> surfaces,
            Transform generatedRoot,
            ref bool warnedAboutBoolean)
        {
            if (generator == null || body == null || generatedRoot == null)
                return;

            FacadeBayCatalog catalog = generator.facadeBayCatalog;
            if (catalog == null)
            {
                Debug.LogWarning(
                    "BuildingGen solid: Facade Bay Planner is enabled but no FacadeBayCatalog is assigned.",
                    generator);
                return;
            }

            Transform oldRoot = generatedRoot.Find(GeneratedRootName);
            if (oldRoot != null)
                Undo.DestroyObjectImmediate(oldRoot.gameObject);

            GameObject rootObject = new GameObject(GeneratedRootName);
            rootObject.transform.SetParent(generatedRoot, false);
            Transform bayRoot = rootObject.transform;

            System.Random random =
                new System.Random(unchecked(generator.seed * 486187739 + 104729));

            for (int i = 0; i < surfaces.Count; i++)
            {
                SolidFacadeSurface surface = surfaces[i];
                if (surface == null)
                    continue;

                FacadeSurfaceMask mask = surface.SurfaceMask;
                if ((generator.facadeBaySurfaces & mask) == 0)
                    continue;

                GenerateForSurface(
                    generator,
                    catalog,
                    random,
                    body,
                    surface,
                    bayRoot,
                    ref warnedAboutBoolean);
            }

            RefreshCollider(body, generator.generateMeshCollider);
        }

        private static void GenerateForSurface(
            BuildingGenerator generator,
            FacadeBayCatalog catalog,
            System.Random random,
            GameObject body,
            SolidFacadeSurface surface,
            Transform bayRoot,
            ref bool warnedAboutBoolean)
        {
            float wallWidth = surface.width;
            float wallHeight = surface.height;
            if (wallWidth < 0.2f || wallHeight < 0.2f)
                return;

            FacadeBayReservationMap reservations = new FacadeBayReservationMap();
            ReserveExistingOpenings(generator, surface, reservations);

            int specialCount =
                reservations.CountKind(FacadeBayKind.Door)
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
                        surface.SurfaceMask,
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
                        body,
                        surface,
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
            SolidFacadeSurface surface,
            FacadeBayReservationMap reservations)
        {
            OpeningDefinition[] existing =
                generator.generatedRoot.GetComponentsInChildren<OpeningDefinition>(true);

            Vector3 right = surface.transform.right.normalized;
            Vector3 forward = surface.transform.forward.normalized;
            float floorHeight = Mathf.Max(
                0.1f,
                generator.floorHeight * generator.transform.TransformVector(Vector3.up).magnitude);

            for (int i = 0; i < existing.Length; i++)
            {
                OpeningDefinition definition = existing[i];
                if (definition == null)
                    continue;

                Vector3 delta = definition.transform.position - surface.transform.position;
                float depth = Mathf.Abs(Vector3.Dot(delta, forward));
                if (depth > Mathf.Max(0.75f, surface.thickness * 2.5f))
                    continue;

                float x = Vector3.Dot(delta, right);
                if (Mathf.Abs(x) > surface.width * 0.65f)
                    continue;

                float localY =
                    generator.transform.InverseTransformPoint(definition.transform.position).y;
                int floor = Mathf.Clamp(
                    Mathf.FloorToInt((localY + 0.01f) / floorHeight),
                    0,
                    Mathf.Max(0, generator.floors - 1));

                FacadeBayKind kind =
                    definition.kind == OpeningKind.Door
                    ? FacadeBayKind.Door
                    : FacadeBayKind.Window;

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
            x = Mathf.Clamp(
                x,
                -wallWidth * 0.5f + half,
                wallWidth * 0.5f - half);
            reservations.TryReserve(
                floor,
                1,
                x,
                width,
                0.05f,
                FacadeBayKind.Blank);
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

                FacadeBayDefinition definition =
                    prefab.GetComponent<FacadeBayDefinition>();

                if (definition == null
                    || !definition.IsAllowed(floor, generator.floors, surface))
                {
                    continue;
                }

                if (IsSpecial(definition.kind)
                    && specialCount >= generator.maxSpecialBaysPerWall)
                {
                    continue;
                }

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
            GameObject body,
            SolidFacadeSurface surface,
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

            instance.name =
                prefab.name + "_F" + floor + "_" + index.ToString("00");

            float baseY =
                floor * generator.floorHeight + sourceDefinition.sillHeight;

            instance.transform.position =
                surface.transform.position
                + surface.transform.right * horizontal
                + surface.transform.up * (baseY - surface.height * 0.5f);
            instance.transform.rotation = surface.transform.rotation;

            FacadeBayDefinition definition =
                instance.GetComponent<FacadeBayDefinition>();
            if (definition == null)
                definition = sourceDefinition;

            if (definition.visualRoot != null
                && Mathf.Abs(definition.visualDepthOffset) > 0.0001f)
            {
                definition.visualRoot.localPosition +=
                    Vector3.forward * definition.visualDepthOffset;
            }

            if (!definition.cutHostSurface)
                return;

            GameObject cutter = definition.CutterObject;
            if (cutter == null)
            {
                Debug.LogWarning(
                    "BuildingGen solid: facade bay prefab '" + prefab.name
                    + "' requests a host cut but has no Cutter Root.",
                    instance);
                return;
            }

            MeshRenderer cutterRenderer = cutter.GetComponent<MeshRenderer>();
            MeshRenderer bodyRenderer = body.GetComponent<MeshRenderer>();
            if (cutterRenderer != null && bodyRenderer != null)
                cutterRenderer.sharedMaterials = bodyRenderer.sharedMaterials;

            string error;
            if (!ProBuilderBooleanBackend.TrySubtract(body, cutter, out error)
                && !warnedAboutBoolean)
            {
                warnedAboutBoolean = true;
                Debug.LogWarning(
                    "BuildingGen solid: facade-bay CSG is unavailable or failed. "
                    + error,
                    generator);
            }

            cutter.SetActive(false);
        }

        private static bool IsSpecial(FacadeBayKind kind)
        {
            return kind == FacadeBayKind.Door
                || kind == FacadeBayKind.Iwan
                || kind == FacadeBayKind.Arcade;
        }

        private static void RefreshCollider(GameObject body, bool enabled)
        {
            MeshFilter filter = body.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return;

            MeshCollider collider = body.GetComponent<MeshCollider>();
            if (!enabled)
            {
                if (collider != null)
                    Object.DestroyImmediate(collider);
                return;
            }

            if (collider == null)
                collider = body.AddComponent<MeshCollider>();

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
