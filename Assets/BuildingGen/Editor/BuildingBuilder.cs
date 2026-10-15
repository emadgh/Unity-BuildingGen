using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emadgh.BuildingGen.Editor
{
    internal enum FacadeSide
    {
        Front,
        Back,
        Left,
        Right
    }

    internal static class BuildingBuilder
    {
        private const string GeneratedRootName = "__GeneratedBuilding";

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
            Undo.RegisterCreatedObjectUndo(root, "Generate building");
            root.transform.SetParent(generator.transform, false);
            generator.generatedRoot = root.transform;

            Transform structureRoot = CreateGroup("Structure", root.transform);
            Transform openingsRoot = CreateGroup("Openings", root.transform);
            Transform roofRoot = CreateGroup("Roof", root.transform);

            System.Random random = new System.Random(generator.seed);
            float totalHeight = generator.TotalHeight;
            float t = generator.wallThickness;
            float halfWidth = generator.width * 0.5f;
            float halfDepth = generator.depth * 0.5f;

            GameObject front = CreateCube(
                "Wall_Front",
                structureRoot,
                new Vector3(0f, totalHeight * 0.5f, halfDepth),
                new Vector3(generator.width, totalHeight, t),
                generator.style.wallMaterial);

            GameObject back = CreateCube(
                "Wall_Back",
                structureRoot,
                new Vector3(0f, totalHeight * 0.5f, -halfDepth),
                new Vector3(generator.width, totalHeight, t),
                generator.style.wallMaterial);

            GameObject left = CreateCube(
                "Wall_Left",
                structureRoot,
                new Vector3(-halfWidth, totalHeight * 0.5f, 0f),
                new Vector3(t, totalHeight, generator.depth),
                generator.style.wallMaterial);

            GameObject right = CreateCube(
                "Wall_Right",
                structureRoot,
                new Vector3(halfWidth, totalHeight * 0.5f, 0f),
                new Vector3(t, totalHeight, generator.depth),
                generator.style.wallMaterial);

            bool warnedAboutBoolean = false;

            if (generator.generateFrontOpenings)
            {
                GenerateFacade(
                    generator,
                    generator.style,
                    random,
                    FacadeSide.Front,
                    front,
                    openingsRoot,
                    generator.width,
                    generator.style.frontWindowChance,
                    generator.generateGroundFloorDoor,
                    ref warnedAboutBoolean);
            }

            if (generator.generateBackOpenings)
            {
                GenerateFacade(
                    generator,
                    generator.style,
                    random,
                    FacadeSide.Back,
                    back,
                    openingsRoot,
                    generator.width,
                    generator.style.backWindowChance,
                    false,
                    ref warnedAboutBoolean);
            }

            if (generator.generateSideOpenings)
            {
                GenerateFacade(
                    generator,
                    generator.style,
                    random,
                    FacadeSide.Left,
                    left,
                    openingsRoot,
                    generator.depth,
                    generator.style.sideWindowChance,
                    false,
                    ref warnedAboutBoolean);

                GenerateFacade(
                    generator,
                    generator.style,
                    random,
                    FacadeSide.Right,
                    right,
                    openingsRoot,
                    generator.depth,
                    generator.style.sideWindowChance,
                    false,
                    ref warnedAboutBoolean);
            }

            FinalizeWall(front);
            FinalizeWall(back);
            FinalizeWall(left);
            FinalizeWall(right);

            GenerateRoof(generator, roofRoot);

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
                Undo.DestroyObjectImmediate(existing.gameObject);

            generator.generatedRoot = null;
            EditorUtility.SetDirty(generator);
        }

        public static GameObject BakeCopy(BuildingGenerator generator)
        {
            if (generator == null || generator.generatedRoot == null)
                return null;

            GameObject baked = Object.Instantiate(generator.generatedRoot.gameObject);
            baked.name = generator.name + "_Baked_" + generator.seed;
            baked.transform.SetParent(generator.transform.parent, true);
            Undo.RegisterCreatedObjectUndo(baked, "Bake generated building");

            OpeningDefinition[] openingDefinitions = baked.GetComponentsInChildren<OpeningDefinition>(true);
            foreach (OpeningDefinition definition in openingDefinitions)
            {
                if (definition.cutterRoot != null)
                    Object.DestroyImmediate(definition.cutterRoot.gameObject);
                Object.DestroyImmediate(definition);
            }

            return baked;
        }

        private static void GenerateFacade(
            BuildingGenerator generator,
            YazdBuildingStyle style,
            System.Random random,
            FacadeSide side,
            GameObject wall,
            Transform openingsRoot,
            float span,
            float facadeChance,
            bool allowDoor,
            ref bool warnedAboutBoolean)
        {
            if (wall == null || facadeChance <= 0f || Next01(random) > facadeChance)
                return;

            float reservedDoorX = float.NaN;
            OpeningDefinition doorDefinition = null;

            if (allowDoor && generator.floors > 0 && style.doorPrefabs.Count > 0)
            {
                GameObject doorPrefab = PickOpeningPrefab(style.doorPrefabs, 0, random, out doorDefinition);
                if (doorPrefab != null && doorDefinition != null)
                {
                    float maxX = Mathf.Max(0f, span * 0.5f - doorDefinition.edgeClearance - doorDefinition.nominalWidth * 0.5f);
                    bool centered = Next01(random) <= style.centeredDoorChance;
                    reservedDoorX = centered ? 0f : Range(random, -maxX, maxX);

                    PlaceOpening(
                        generator,
                        wall,
                        openingsRoot,
                        side,
                        doorPrefab,
                        doorDefinition,
                        reservedDoorX,
                        0f,
                        random,
                        ref warnedAboutBoolean);
                }
            }

            int minWindows = Mathf.Max(0, generator.windowsPerFloor.x);
            int maxWindows = Mathf.Max(minWindows, generator.windowsPerFloor.y);

            for (int floor = 0; floor < generator.floors; floor++)
            {
                int count = random.Next(minWindows, maxWindows + 1);
                if (count <= 0 || style.windowPrefabs.Count == 0)
                    continue;

                float step = span / (count + 1f);
                for (int index = 0; index < count; index++)
                {
                    GameObject prefab = PickOpeningPrefab(style.windowPrefabs, floor, random, out OpeningDefinition definition);
                    if (prefab == null || definition == null)
                        continue;

                    float x = -span * 0.5f + step * (index + 1);
                    float maxJitter = step * generator.horizontalJitter;
                    x += Range(random, -maxJitter, maxJitter);

                    float halfOpening = definition.nominalWidth * 0.5f;
                    float edge = Mathf.Max(definition.edgeClearance, 0.1f);
                    x = Mathf.Clamp(x, -span * 0.5f + edge + halfOpening, span * 0.5f - edge - halfOpening);

                    if (floor == 0 && doorDefinition != null && !float.IsNaN(reservedDoorX))
                    {
                        float required = halfOpening + doorDefinition.nominalWidth * 0.5f +
                                         Mathf.Max(definition.neighborClearance, doorDefinition.neighborClearance);
                        if (Mathf.Abs(x - reservedDoorX) < required)
                            continue;
                    }

                    float baseY = floor * generator.floorHeight + definition.sillHeight;
                    PlaceOpening(
                        generator,
                        wall,
                        openingsRoot,
                        side,
                        prefab,
                        definition,
                        x,
                        baseY,
                        random,
                        ref warnedAboutBoolean);
                }
            }
        }

        private static void PlaceOpening(
            BuildingGenerator generator,
            GameObject wall,
            Transform openingsRoot,
            FacadeSide side,
            GameObject prefab,
            OpeningDefinition sourceDefinition,
            float horizontal,
            float baseY,
            System.Random random,
            ref bool warnedAboutBoolean)
        {
            GameObject instance;
            if (PrefabUtility.IsPartOfPrefabAsset(prefab))
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, openingsRoot);
            else
                instance = Object.Instantiate(prefab, openingsRoot);

            instance.name = prefab.name;
            instance.transform.localRotation = Quaternion.Euler(0f, FacadeRotation(side), 0f);

            float yJitter = generator.floorHeight * generator.sillJitter;
            float y = Mathf.Max(0f, baseY + Range(random, -yJitter, yJitter));
            instance.transform.localPosition = FacadePosition(generator, side, horizontal, y);

            OpeningDefinition definition = instance.GetComponent<OpeningDefinition>();
            if (definition == null)
                definition = sourceDefinition;

            GameObject cutter = definition != null ? definition.CutterObject : null;
            if (cutter == null)
            {
                Debug.LogWarning("BuildingGen: opening prefab '" + prefab.name + "' has no cutterRoot.", instance);
                return;
            }

            MeshRenderer cutterRenderer = cutter.GetComponent<MeshRenderer>();
            MeshRenderer wallRenderer = wall.GetComponent<MeshRenderer>();
            if (cutterRenderer != null && wallRenderer != null)
                cutterRenderer.sharedMaterials = wallRenderer.sharedMaterials;

            string error;
            bool cut = ProBuilderBooleanBackend.TrySubtract(wall, cutter, out error);
            if (!cut && !warnedAboutBoolean)
            {
                warnedAboutBoolean = true;
                Debug.LogWarning("BuildingGen: CSG subtraction is not active. Visual prefabs were placed but the walls were not cut. " + error, generator);
            }

            cutter.SetActive(false);
        }

        private static Vector3 FacadePosition(BuildingGenerator generator, FacadeSide side, float horizontal, float y)
        {
            switch (side)
            {
                case FacadeSide.Front:
                    return new Vector3(horizontal, y, generator.depth * 0.5f);
                case FacadeSide.Back:
                    return new Vector3(-horizontal, y, -generator.depth * 0.5f);
                case FacadeSide.Left:
                    return new Vector3(-generator.width * 0.5f, y, horizontal);
                case FacadeSide.Right:
                    return new Vector3(generator.width * 0.5f, y, -horizontal);
                default:
                    return Vector3.zero;
            }
        }

        private static float FacadeRotation(FacadeSide side)
        {
            switch (side)
            {
                case FacadeSide.Front: return 0f;
                case FacadeSide.Back: return 180f;
                case FacadeSide.Left: return -90f;
                case FacadeSide.Right: return 90f;
                default: return 0f;
            }
        }

        private static GameObject PickOpeningPrefab(
            List<GameObject> prefabs,
            int floor,
            System.Random random,
            out OpeningDefinition selectedDefinition)
        {
            selectedDefinition = null;
            float totalWeight = 0f;
            List<GameObject> candidates = new List<GameObject>();
            List<OpeningDefinition> definitions = new List<OpeningDefinition>();

            foreach (GameObject prefab in prefabs)
            {
                if (prefab == null)
                    continue;

                OpeningDefinition definition = prefab.GetComponent<OpeningDefinition>();
                if (definition == null || !definition.IsAllowedOnFloor(floor))
                    continue;

                candidates.Add(prefab);
                definitions.Add(definition);
                totalWeight += Mathf.Max(0.001f, definition.selectionWeight);
            }

            if (candidates.Count == 0)
                return null;

            float pick = Range(random, 0f, totalWeight);
            float cursor = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                cursor += Mathf.Max(0.001f, definitions[i].selectionWeight);
                if (pick <= cursor)
                {
                    selectedDefinition = definitions[i];
                    return candidates[i];
                }
            }

            selectedDefinition = definitions[definitions.Count - 1];
            return candidates[candidates.Count - 1];
        }

        private static void GenerateRoof(BuildingGenerator generator, Transform roofRoot)
        {
            YazdBuildingStyle style = generator.style;
            if (style == null || !style.generateFlatRoof)
                return;

            float y = generator.TotalHeight + style.roofThickness * 0.5f;
            CreateCube(
                "RoofSlab",
                roofRoot,
                new Vector3(0f, y, 0f),
                new Vector3(generator.width, style.roofThickness, generator.depth),
                style.roofMaterial != null ? style.roofMaterial : style.wallMaterial);

            if (!style.generateParapet)
                return;

            float py = generator.TotalHeight + style.roofThickness + style.parapetHeight * 0.5f;
            float pt = style.parapetThickness;

            CreateCube("Parapet_Front", roofRoot,
                new Vector3(0f, py, generator.depth * 0.5f - pt * 0.5f),
                new Vector3(generator.width, style.parapetHeight, pt), style.wallMaterial);
            CreateCube("Parapet_Back", roofRoot,
                new Vector3(0f, py, -generator.depth * 0.5f + pt * 0.5f),
                new Vector3(generator.width, style.parapetHeight, pt), style.wallMaterial);
            CreateCube("Parapet_Left", roofRoot,
                new Vector3(-generator.width * 0.5f + pt * 0.5f, py, 0f),
                new Vector3(pt, style.parapetHeight, generator.depth), style.wallMaterial);
            CreateCube("Parapet_Right", roofRoot,
                new Vector3(generator.width * 0.5f - pt * 0.5f, py, 0f),
                new Vector3(pt, style.parapetHeight, generator.depth), style.wallMaterial);
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject CreateCube(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            Collider primitiveCollider = go.GetComponent<Collider>();
            if (primitiveCollider != null)
                Object.DestroyImmediate(primitiveCollider);

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;

            return go;
        }

        private static void FinalizeWall(GameObject wall)
        {
            if (wall == null)
                return;

            MeshFilter filter = wall.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return;

            MeshCollider collider = wall.GetComponent<MeshCollider>();
            if (collider == null)
                collider = wall.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
        }

        private static float Next01(System.Random random)
        {
            return (float)random.NextDouble();
        }

        private static float Range(System.Random random, float min, float max)
        {
            return Mathf.Lerp(min, max, Next01(random));
        }
    }
}
