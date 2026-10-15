using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emadgh.BuildingGen.Editor
{
    /// <summary>
    /// v0.6 non-modular building backend.
    /// Generates one procedural MeshFilter/MeshRenderer/MeshCollider body and uses
    /// logical facade anchors only for planning doors, windows and facade bays.
    /// </summary>
    internal static class SolidBuildingBuilder
    {
        private const string GeneratedRootName = "__GeneratedBuilding";
        private const string SolidBodyName = "SolidBuildingMesh";
        private const string FacadeRootName = "FacadeSurfaces";

        public static void Generate(BuildingGenerator generator)
        {
            if (generator == null)
                return;

            if (generator.style == null)
            {
                Debug.LogError("BuildingGen: assign a YazdBuildingStyle before generating.", generator);
                return;
            }

            Clear(generator);

            GameObject root = new GameObject(GeneratedRootName);
            Undo.RegisterCreatedObjectUndo(root, "Generate solid building");
            root.transform.SetParent(generator.transform, false);
            generator.generatedRoot = root.transform;

            GameObject body = CreateSolidBody(generator, root.transform, out List<SolidFacadeDescriptor> descriptors);
            List<SolidFacadeSurface> surfaces = CreateFacadeSurfaces(generator, body, root.transform, descriptors);

            Transform detailsRoot = CreateGroup("ArchitecturalDetails", root.transform);
            Transform openingsRoot = CreateGroup("Openings", detailsRoot);
            Transform modulesRoot = CreateGroup("RoofModules", detailsRoot);

            System.Random random = new System.Random(generator.seed);
            bool warnedAboutBoolean = false;

            if (generator.useFacadeBayPlanner)
            {
                if (generator.keepPrimaryEntranceDoor && generator.generateGroundFloorDoor)
                    GeneratePrimaryEntrance(generator, body, surfaces, openingsRoot, random, ref warnedAboutBoolean);

                SolidFacadeBayBuildingBridge.Generate(
                    generator,
                    body,
                    surfaces,
                    root.transform,
                    ref warnedAboutBoolean);
            }
            else
            {
                GenerateLegacyOpenings(
                    generator,
                    body,
                    surfaces,
                    openingsRoot,
                    random,
                    ref warnedAboutBoolean);
            }

            GenerateSimpleRoofModules(generator, modulesRoot, random);
            RefreshCollider(body, generator.generateMeshCollider);
            EditorUtility.SetDirty(generator);
        }

        public static void Clear(BuildingGenerator generator)
        {
            if (generator == null)
                return;

            Transform existing = generator.generatedRoot;
            if (existing == null)
                existing = generator.transform.Find(GeneratedRootName);

            if (existing != null)
            {
                MeshFilter[] filters = existing.GetComponentsInChildren<MeshFilter>(true);
                for (int i = 0; i < filters.Length; i++)
                {
                    MeshFilter filter = filters[i];
                    if (filter != null && filter.sharedMesh != null && !AssetDatabase.Contains(filter.sharedMesh))
                        Object.DestroyImmediate(filter.sharedMesh);
                }

                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            generator.generatedRoot = null;
            EditorUtility.SetDirty(generator);
        }

        public static GameObject BakeCopy(BuildingGenerator generator)
        {
            if (generator == null || generator.generatedRoot == null)
                return null;

            GameObject baked = Object.Instantiate(generator.generatedRoot.gameObject);
            baked.name = generator.name + "_BakedSolid_" + generator.seed;
            baked.transform.SetParent(generator.transform.parent, true);
            Undo.RegisterCreatedObjectUndo(baked, "Bake solid building");

            MeshFilter[] filters = baked.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter.sharedMesh == null)
                    continue;

                Mesh copy = Object.Instantiate(filter.sharedMesh);
                copy.name = filter.sharedMesh.name + "_Baked";
                filter.sharedMesh = copy;

                MeshCollider collider = filter.GetComponent<MeshCollider>();
                if (collider != null)
                    collider.sharedMesh = copy;
            }

            Transform facadeRoot = baked.transform.Find(FacadeRootName);
            if (facadeRoot != null)
                Object.DestroyImmediate(facadeRoot.gameObject);

            OpeningDefinition[] openingDefinitions = baked.GetComponentsInChildren<OpeningDefinition>(true);
            for (int i = 0; i < openingDefinitions.Length; i++)
            {
                OpeningDefinition definition = openingDefinitions[i];
                if (definition.cutterRoot != null)
                    Object.DestroyImmediate(definition.cutterRoot.gameObject);
                Object.DestroyImmediate(definition);
            }

            FacadeBayDefinition[] bayDefinitions = baked.GetComponentsInChildren<FacadeBayDefinition>(true);
            for (int i = 0; i < bayDefinitions.Length; i++)
            {
                FacadeBayDefinition definition = bayDefinitions[i];
                if (definition.cutterRoot != null)
                    Object.DestroyImmediate(definition.cutterRoot.gameObject);
                Object.DestroyImmediate(definition);
            }

            return baked;
        }

        private static GameObject CreateSolidBody(
            BuildingGenerator generator,
            Transform parent,
            out List<SolidFacadeDescriptor> descriptors)
        {
            Mesh mesh = SolidBuildingMeshBuilder.Build(generator, out descriptors);

            GameObject body = new GameObject(SolidBodyName);
            body.transform.SetParent(parent, false);

            MeshFilter filter = body.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = body.AddComponent<MeshRenderer>();
            Material wall = generator.style.wallMaterial;
            Material roof = generator.style.roofMaterial != null ? generator.style.roofMaterial : wall;
            renderer.sharedMaterials = new[] { wall, roof };

            if (generator.generateMeshCollider)
            {
                MeshCollider collider = body.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }

            return body;
        }

        private static List<SolidFacadeSurface> CreateFacadeSurfaces(
            BuildingGenerator generator,
            GameObject body,
            Transform parent,
            List<SolidFacadeDescriptor> descriptors)
        {
            Transform surfaceRoot = CreateGroup(FacadeRootName, parent);
            List<SolidFacadeSurface> result = new List<SolidFacadeSurface>(descriptors.Count);

            for (int i = 0; i < descriptors.Count; i++)
            {
                SolidFacadeDescriptor descriptor = descriptors[i];
                Vector2 mid = descriptor.Midpoint;
                Vector3 outward = new Vector3(descriptor.outward.x, 0f, descriptor.outward.y).normalized;

                GameObject go = new GameObject(
                    (descriptor.courtyard ? "CourtyardFacade_" : "Facade_") + i.ToString("00"));
                go.transform.SetParent(surfaceRoot, false);
                go.transform.localPosition = new Vector3(mid.x, generator.TotalHeight * 0.5f, mid.y);
                go.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);

                SolidFacadeSurface surface = go.AddComponent<SolidFacadeSurface>();
                surface.role = descriptor.role;
                surface.courtyard = descriptor.courtyard;
                surface.width = descriptor.Width;
                surface.height = generator.TotalHeight;
                surface.thickness = generator.wallThickness;
                surface.solidBody = body;
                result.Add(surface);
            }

            return result;
        }

        private static void GenerateLegacyOpenings(
            BuildingGenerator generator,
            GameObject body,
            List<SolidFacadeSurface> surfaces,
            Transform parent,
            System.Random random,
            ref bool warnedAboutBoolean)
        {
            SolidFacadeSurface entranceSurface = FindPrimaryEntranceSurface(surfaces);
            OpeningDefinition entranceDefinition = null;
            float entranceX = float.NaN;

            if (generator.generateGroundFloorDoor && entranceSurface != null && generator.style.doorPrefabs.Count > 0)
            {
                GameObject doorPrefab = PickOpeningPrefab(
                    generator.style.doorPrefabs,
                    0,
                    random,
                    out entranceDefinition);

                if (doorPrefab != null && entranceDefinition != null)
                {
                    float maxX = Mathf.Max(
                        0f,
                        entranceSurface.width * 0.5f
                        - entranceDefinition.edgeClearance
                        - entranceDefinition.nominalWidth * 0.5f);

                    bool centered = Next01(random) <= generator.style.centeredDoorChance;
                    entranceX = centered ? 0f : Range(random, -maxX, maxX);

                    PlaceOpening(
                        generator,
                        body,
                        entranceSurface,
                        parent,
                        doorPrefab,
                        entranceDefinition,
                        entranceX,
                        0,
                        true,
                        random,
                        ref warnedAboutBoolean);
                }
            }

            for (int surfaceIndex = 0; surfaceIndex < surfaces.Count; surfaceIndex++)
            {
                SolidFacadeSurface surface = surfaces[surfaceIndex];
                float chance = GetFacadeChance(generator, surface);
                if (chance <= 0f || Next01(random) > chance)
                    continue;

                int minWindows = Mathf.Max(0, generator.windowsPerFloor.x);
                int maxWindows = Mathf.Max(minWindows, generator.windowsPerFloor.y);

                for (int floor = 0; floor < generator.floors; floor++)
                {
                    int count = random.Next(minWindows, maxWindows + 1);
                    if (count <= 0 || generator.style.windowPrefabs.Count == 0)
                        continue;

                    float step = surface.width / (count + 1f);
                    for (int index = 0; index < count; index++)
                    {
                        GameObject prefab = PickOpeningPrefab(
                            generator.style.windowPrefabs,
                            floor,
                            random,
                            out OpeningDefinition definition);

                        if (prefab == null || definition == null)
                            continue;

                        float x = -surface.width * 0.5f + step * (index + 1);
                        x += Range(
                            random,
                            -step * generator.horizontalJitter,
                            step * generator.horizontalJitter);

                        float halfOpening = definition.nominalWidth * 0.5f;
                        float edge = Mathf.Max(0.1f, definition.edgeClearance);
                        float minX = -surface.width * 0.5f + edge + halfOpening;
                        float maxX = surface.width * 0.5f - edge - halfOpening;
                        if (minX > maxX)
                            continue;

                        x = Mathf.Clamp(x, minX, maxX);

                        if (floor == 0
                            && surface == entranceSurface
                            && entranceDefinition != null
                            && !float.IsNaN(entranceX))
                        {
                            float minDistance =
                                entranceDefinition.nominalWidth * 0.5f
                                + definition.nominalWidth * 0.5f
                                + Mathf.Max(
                                    entranceDefinition.neighborClearance,
                                    definition.neighborClearance);

                            if (Mathf.Abs(x - entranceX) < minDistance)
                                continue;
                        }

                        PlaceOpening(
                            generator,
                            body,
                            surface,
                            parent,
                            prefab,
                            definition,
                            x,
                            floor,
                            false,
                            random,
                            ref warnedAboutBoolean);
                    }
                }
            }
        }

        private static void GeneratePrimaryEntrance(
            BuildingGenerator generator,
            GameObject body,
            List<SolidFacadeSurface> surfaces,
            Transform parent,
            System.Random random,
            ref bool warnedAboutBoolean)
        {
            SolidFacadeSurface entranceSurface = FindPrimaryEntranceSurface(surfaces);
            if (entranceSurface == null || generator.style.doorPrefabs.Count == 0)
                return;

            GameObject prefab = PickOpeningPrefab(
                generator.style.doorPrefabs,
                0,
                random,
                out OpeningDefinition definition);

            if (prefab == null || definition == null)
                return;

            float maxX = Mathf.Max(
                0f,
                entranceSurface.width * 0.5f
                - definition.edgeClearance
                - definition.nominalWidth * 0.5f);

            float x = Next01(random) <= generator.style.centeredDoorChance
                ? 0f
                : Range(random, -maxX, maxX);

            PlaceOpening(
                generator,
                body,
                entranceSurface,
                parent,
                prefab,
                definition,
                x,
                0,
                true,
                random,
                ref warnedAboutBoolean);
        }

        private static void PlaceOpening(
            BuildingGenerator generator,
            GameObject body,
            SolidFacadeSurface surface,
            Transform parent,
            GameObject prefab,
            OpeningDefinition sourceDefinition,
            float horizontal,
            int floor,
            bool primaryDoor,
            System.Random random,
            ref bool warnedAboutBoolean)
        {
            GameObject instance = InstantiatePrefab(prefab, parent);
            instance.name = prefab.name + "_F" + floor;

            float sill = sourceDefinition.sillHeight;
            if (!primaryDoor && generator.sillJitter > 0f)
                sill += Range(random, -generator.sillJitter, generator.sillJitter);

            float baseY = floor * generator.floorHeight + sill;
            instance.transform.position =
                surface.transform.position
                + surface.transform.right * horizontal
                + surface.transform.up * (baseY - surface.height * 0.5f);
            instance.transform.rotation = surface.transform.rotation;

            OpeningDefinition definition = instance.GetComponent<OpeningDefinition>();
            if (definition == null)
                definition = sourceDefinition;

            if (primaryDoor
                && generator.recessedEntrance
                && definition.visualRoot != null
                && generator.entranceRecess > 0f)
            {
                definition.visualRoot.localPosition += Vector3.back * generator.entranceRecess;
            }

            GameObject cutter = definition.CutterObject;
            if (cutter == null)
            {
                Debug.LogWarning(
                    "BuildingGen solid: opening prefab '" + prefab.name + "' has no cutterRoot.",
                    instance);
                return;
            }

            ApplyBoolean(
                generator,
                body,
                cutter,
                ref warnedAboutBoolean,
                "opening prefab '" + prefab.name + "'");

            cutter.SetActive(false);
        }

        private static GameObject PickOpeningPrefab(
            List<GameObject> prefabs,
            int floor,
            System.Random random,
            out OpeningDefinition definition)
        {
            definition = null;
            float total = 0f;
            List<GameObject> candidates = new List<GameObject>();
            List<OpeningDefinition> definitions = new List<OpeningDefinition>();
            List<float> weights = new List<float>();

            for (int i = 0; i < prefabs.Count; i++)
            {
                GameObject prefab = prefabs[i];
                if (prefab == null)
                    continue;

                OpeningDefinition candidate = prefab.GetComponent<OpeningDefinition>();
                if (candidate == null || !candidate.IsAllowedOnFloor(floor))
                    continue;

                float weight = Mathf.Max(0.001f, candidate.selectionWeight);
                candidates.Add(prefab);
                definitions.Add(candidate);
                weights.Add(weight);
                total += weight;
            }

            if (candidates.Count == 0)
                return null;

            float pick = Range(random, 0f, total);
            float cursor = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                cursor += weights[i];
                if (pick <= cursor)
                {
                    definition = definitions[i];
                    return candidates[i];
                }
            }

            definition = definitions[definitions.Count - 1];
            return candidates[candidates.Count - 1];
        }

        private static SolidFacadeSurface FindPrimaryEntranceSurface(List<SolidFacadeSurface> surfaces)
        {
            SolidFacadeSurface best = null;
            float bestFront = float.MinValue;
            float bestWidth = -1f;

            for (int i = 0; i < surfaces.Count; i++)
            {
                SolidFacadeSurface surface = surfaces[i];
                if (surface == null || surface.courtyard || surface.role != SolidFacadeRole.Front)
                    continue;

                float front = surface.transform.localPosition.z;
                if (front > bestFront + 0.01f
                    || (Mathf.Abs(front - bestFront) < 0.01f && surface.width > bestWidth))
                {
                    best = surface;
                    bestFront = front;
                    bestWidth = surface.width;
                }
            }

            return best;
        }

        private static float GetFacadeChance(BuildingGenerator generator, SolidFacadeSurface surface)
        {
            if (surface.role == SolidFacadeRole.Courtyard)
                return generator.generateCourtyardOpenings ? generator.style.courtyardWindowChance : 0f;

            if (surface.role == SolidFacadeRole.Front)
                return generator.generateFrontOpenings ? generator.style.frontWindowChance : 0f;

            if (surface.role == SolidFacadeRole.Back)
                return generator.generateBackOpenings ? generator.style.backWindowChance : 0f;

            return generator.generateSideOpenings ? generator.style.sideWindowChance : 0f;
        }

        private static void GenerateSimpleRoofModules(
            BuildingGenerator generator,
            Transform parent,
            System.Random random)
        {
            if (generator.generateBadgir
                && generator.badgirCount > 0
                && generator.style.badgirPrefabs.Count > 0)
            {
                int placed = 0;
                for (int attempt = 0; attempt < generator.badgirCount * 5 && placed < generator.badgirCount; attempt++)
                {
                    if (Next01(random) > generator.style.badgirChance)
                        continue;

                    GameObject prefab = PickModulePrefab(generator.style.badgirPrefabs, random);
                    if (prefab == null)
                        continue;

                    Vector3 local;
                    if (!TryPickRoofPoint(generator, random, out local))
                        continue;

                    GameObject instance = InstantiatePrefab(prefab, parent);
                    instance.name = prefab.name + "_" + placed;
                    instance.transform.localPosition = local + Vector3.up * generator.TotalHeight;
                    instance.transform.localRotation = Quaternion.Euler(0f, random.Next(0, 4) * 90f, 0f);
                    placed++;
                }
            }

            if (!generator.generateRoofForms || generator.maxRoofForms <= 0)
                return;

            List<GameObject> roofPool = new List<GameObject>();
            roofPool.AddRange(generator.style.domePrefabs);
            roofPool.AddRange(generator.style.vaultPrefabs);
            if (roofPool.Count == 0)
                return;

            int count = 0;
            for (int attempt = 0; attempt < generator.maxRoofForms * 5 && count < generator.maxRoofForms; attempt++)
            {
                if (Next01(random) > generator.roofFormChance)
                    continue;

                GameObject prefab = PickModulePrefab(roofPool, random);
                if (prefab == null)
                    continue;

                Vector3 local;
                if (!TryPickRoofPoint(generator, random, out local))
                    continue;

                GameObject instance = InstantiatePrefab(prefab, parent);
                instance.name = prefab.name + "_" + count;
                instance.transform.localPosition = local + Vector3.up * generator.TotalHeight;
                instance.transform.localRotation = Quaternion.Euler(0f, random.Next(0, 4) * 90f, 0f);
                count++;
            }
        }

        private static bool TryPickRoofPoint(
            BuildingGenerator generator,
            System.Random random,
            out Vector3 local)
        {
            float inset = Mathf.Max(generator.wallThickness * 2f, generator.style.badgirRoofInset);
            float hx = Mathf.Max(0.1f, generator.width * 0.5f - inset);
            float hz = Mathf.Max(0.1f, generator.depth * 0.5f - inset);

            for (int attempt = 0; attempt < 20; attempt++)
            {
                float x = Range(random, -hx, hx);
                float z = Range(random, -hz, hz);

                if (!SolidBuildingMeshBuilder.ContainsFootprintPoint(
                        generator,
                        new Vector2(x, z)))
                {
                    continue;
                }

                local = new Vector3(x, 0f, z);
                return true;
            }

            local = Vector3.zero;
            return false;
        }

        private static GameObject PickModulePrefab(List<GameObject> prefabs, System.Random random)
        {
            List<GameObject> candidates = new List<GameObject>();
            List<float> weights = new List<float>();
            float total = 0f;

            for (int i = 0; i < prefabs.Count; i++)
            {
                GameObject prefab = prefabs[i];
                if (prefab == null)
                    continue;

                YazdModuleDefinition definition = prefab.GetComponent<YazdModuleDefinition>();
                float weight = definition != null ? Mathf.Max(0.001f, definition.selectionWeight) : 1f;
                candidates.Add(prefab);
                weights.Add(weight);
                total += weight;
            }

            if (candidates.Count == 0)
                return null;

            float pick = Range(random, 0f, total);
            float cursor = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                cursor += weights[i];
                if (pick <= cursor)
                    return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        private static void ApplyBoolean(
            BuildingGenerator generator,
            GameObject body,
            GameObject cutter,
            ref bool warnedAboutBoolean,
            string context)
        {
            MeshRenderer cutterRenderer = cutter.GetComponent<MeshRenderer>();
            MeshRenderer bodyRenderer = body.GetComponent<MeshRenderer>();
            if (cutterRenderer != null && bodyRenderer != null)
                cutterRenderer.sharedMaterials = bodyRenderer.sharedMaterials;

            string error;
            if (!ProBuilderBooleanBackend.TrySubtract(body, cutter, out error) && !warnedAboutBoolean)
            {
                warnedAboutBoolean = true;
                Debug.LogWarning(
                    "BuildingGen solid: CSG subtraction is unavailable or failed for "
                    + context + ". " + error,
                    generator);
            }
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

        private static Transform CreateGroup(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(prefab))
                return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);

            return Object.Instantiate(prefab, parent);
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
