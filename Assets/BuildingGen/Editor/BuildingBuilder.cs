using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emadgh.BuildingGen.Editor
{
    internal enum FacadeRole
    {
        Front,
        Back,
        Side,
        Courtyard
    }

    internal struct WallSegment
    {
        public string name;
        public Vector2 start;
        public Vector2 end;
        public Vector2 outward;
        public FacadeRole role;
        public bool courtyard;

        public float Length { get { return Vector2.Distance(start, end); } }
        public Vector2 Midpoint { get { return (start + end) * 0.5f; } }
    }

    internal struct MassRect
    {
        public Vector2 center;
        public Vector2 size;

        public MassRect(Vector2 center, Vector2 size)
        {
            this.center = center;
            this.size = size;
        }
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
            Transform modulesRoot = CreateGroup("YazdModules", root.transform);
            Transform roofRoot = CreateGroup("Roof", root.transform);

            System.Random random = new System.Random(generator.seed);
            List<WallSegment> segments = BuildWallSegments(generator);
            List<MassRect> masses = BuildMassRects(generator);
            List<GameObject> walls = new List<GameObject>();

            for (int i = 0; i < segments.Count; i++)
                walls.Add(CreateWall(generator, segments[i], structureRoot));

            int entranceIndex = FindPrimaryEntranceSegment(segments);
            bool warnedAboutBoolean = false;

            for (int i = 0; i < segments.Count; i++)
            {
                GenerateFacade(
                    generator,
                    generator.style,
                    random,
                    segments[i],
                    walls[i],
                    openingsRoot,
                    i == entranceIndex && generator.generateGroundFloorDoor,
                    ref warnedAboutBoolean);
            }

            for (int i = 0; i < walls.Count; i++)
                FinalizeWall(walls[i]);

            GenerateRoof(generator, roofRoot, masses, segments);
            GenerateIwan(generator, random, segments, modulesRoot);
            GenerateBadgirs(generator, random, masses, modulesRoot);

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

            YazdModuleDefinition[] modules = baked.GetComponentsInChildren<YazdModuleDefinition>(true);
            foreach (YazdModuleDefinition module in modules)
                Object.DestroyImmediate(module);

            return baked;
        }

        private static List<WallSegment> BuildWallSegments(BuildingGenerator generator)
        {
            List<WallSegment> result = new List<WallSegment>();
            float hw = generator.width * 0.5f;
            float hd = generator.depth * 0.5f;
            float wing = Mathf.Clamp(generator.wingWidth, 1.5f, Mathf.Min(generator.width, generator.depth) * 0.45f);

            if (generator.footprint == BuildingFootprint.Rectangle || generator.footprint == BuildingFootprint.Courtyard)
            {
                AddLoop(result, new[]
                {
                    new Vector2(-hw, -hd),
                    new Vector2(hw, -hd),
                    new Vector2(hw, hd),
                    new Vector2(-hw, hd)
                }, false, "Outer");
            }
            else if (generator.footprint == BuildingFootprint.LShape)
            {
                AddLoop(result, new[]
                {
                    new Vector2(-hw, -hd),
                    new Vector2(hw, -hd),
                    new Vector2(hw, -hd + wing),
                    new Vector2(-hw + wing, -hd + wing),
                    new Vector2(-hw + wing, hd),
                    new Vector2(-hw, hd)
                }, false, "OuterL");
            }
            else
            {
                float recess = Mathf.Clamp(generator.uRecessDepth, 1.5f, generator.depth - generator.wallThickness * 4f);
                float innerBackZ = hd - recess;
                AddLoop(result, new[]
                {
                    new Vector2(-hw, -hd),
                    new Vector2(hw, -hd),
                    new Vector2(hw, hd),
                    new Vector2(hw - wing, hd),
                    new Vector2(hw - wing, innerBackZ),
                    new Vector2(-hw + wing, innerBackZ),
                    new Vector2(-hw + wing, hd),
                    new Vector2(-hw, hd)
                }, false, "OuterU");
            }

            if (generator.footprint == BuildingFootprint.Courtyard)
            {
                float cw = Mathf.Min(generator.courtyardWidth, generator.width - generator.wallThickness * 6f);
                float cd = Mathf.Min(generator.courtyardDepth, generator.depth - generator.wallThickness * 6f);
                float chw = cw * 0.5f;
                float chd = cd * 0.5f;

                AddLoop(result, new[]
                {
                    new Vector2(-chw, -chd),
                    new Vector2(chw, -chd),
                    new Vector2(chw, chd),
                    new Vector2(-chw, chd)
                }, true, "Courtyard");
            }

            return result;
        }

        private static void AddLoop(List<WallSegment> result, Vector2[] points, bool courtyard, string prefix)
        {
            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < points.Length; i++)
                centroid += points[i];
            centroid /= points.Length;

            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Length];
                Vector2 direction = (b - a).normalized;
                Vector2 normal = new Vector2(direction.y, -direction.x);
                Vector2 midpoint = (a + b) * 0.5f;
                Vector2 desired = courtyard ? centroid - midpoint : midpoint - centroid;
                if (Vector2.Dot(normal, desired) < 0f)
                    normal = -normal;

                FacadeRole role = courtyard ? FacadeRole.Courtyard : ClassifyFacade(normal);
                result.Add(new WallSegment
                {
                    name = prefix + "_Wall_" + i,
                    start = a,
                    end = b,
                    outward = normal,
                    role = role,
                    courtyard = courtyard
                });
            }
        }

        private static FacadeRole ClassifyFacade(Vector2 normal)
        {
            if (normal.y > 0.55f)
                return FacadeRole.Front;
            if (normal.y < -0.55f)
                return FacadeRole.Back;
            return FacadeRole.Side;
        }

        private static List<MassRect> BuildMassRects(BuildingGenerator generator)
        {
            List<MassRect> result = new List<MassRect>();
            float w = generator.width;
            float d = generator.depth;
            float hw = w * 0.5f;
            float hd = d * 0.5f;
            float wing = Mathf.Clamp(generator.wingWidth, 1.5f, Mathf.Min(w, d) * 0.45f);

            if (generator.footprint == BuildingFootprint.Rectangle)
            {
                result.Add(new MassRect(Vector2.zero, new Vector2(w, d)));
            }
            else if (generator.footprint == BuildingFootprint.LShape)
            {
                result.Add(new MassRect(new Vector2(0f, -hd + wing * 0.5f), new Vector2(w, wing)));
                result.Add(new MassRect(new Vector2(-hw + wing * 0.5f, wing * 0.5f), new Vector2(wing, d - wing)));
            }
            else if (generator.footprint == BuildingFootprint.UShape)
            {
                float recess = Mathf.Clamp(generator.uRecessDepth, 1.5f, d - generator.wallThickness * 4f);
                float rearDepth = Mathf.Max(generator.wallThickness * 2f, d - recess);
                result.Add(new MassRect(new Vector2(0f, -hd + rearDepth * 0.5f), new Vector2(w, rearDepth)));
                result.Add(new MassRect(new Vector2(-hw + wing * 0.5f, hd - recess * 0.5f), new Vector2(wing, recess)));
                result.Add(new MassRect(new Vector2(hw - wing * 0.5f, hd - recess * 0.5f), new Vector2(wing, recess)));
            }
            else
            {
                float cw = Mathf.Min(generator.courtyardWidth, w - generator.wallThickness * 6f);
                float cd = Mathf.Min(generator.courtyardDepth, d - generator.wallThickness * 6f);
                float horizontalStrip = Mathf.Max(generator.wallThickness * 2f, (d - cd) * 0.5f);
                float verticalStrip = Mathf.Max(generator.wallThickness * 2f, (w - cw) * 0.5f);

                result.Add(new MassRect(new Vector2(0f, hd - horizontalStrip * 0.5f), new Vector2(w, horizontalStrip)));
                result.Add(new MassRect(new Vector2(0f, -hd + horizontalStrip * 0.5f), new Vector2(w, horizontalStrip)));
                result.Add(new MassRect(new Vector2(-hw + verticalStrip * 0.5f, 0f), new Vector2(verticalStrip, cd)));
                result.Add(new MassRect(new Vector2(hw - verticalStrip * 0.5f, 0f), new Vector2(verticalStrip, cd)));
            }

            return result;
        }

        private static GameObject CreateWall(BuildingGenerator generator, WallSegment segment, Transform parent)
        {
            Vector2 mid = segment.Midpoint;
            Vector3 position = new Vector3(mid.x, generator.TotalHeight * 0.5f, mid.y);
            Vector3 forward = new Vector3(segment.outward.x, 0f, segment.outward.y);

            GameObject wall = CreateCube(
                segment.name,
                parent,
                position,
                new Vector3(segment.Length, generator.TotalHeight, generator.wallThickness),
                generator.style.wallMaterial);

            wall.transform.localRotation = Quaternion.LookRotation(forward, Vector3.up);
            return wall;
        }

        private static int FindPrimaryEntranceSegment(List<WallSegment> segments)
        {
            int best = -1;
            float bestFront = float.MinValue;
            float bestLength = -1f;

            for (int i = 0; i < segments.Count; i++)
            {
                WallSegment segment = segments[i];
                if (segment.courtyard || segment.role != FacadeRole.Front)
                    continue;

                float front = segment.Midpoint.y;
                if (front > bestFront + 0.01f || (Mathf.Abs(front - bestFront) < 0.01f && segment.Length > bestLength))
                {
                    best = i;
                    bestFront = front;
                    bestLength = segment.Length;
                }
            }

            return best;
        }

        private static void GenerateFacade(
            BuildingGenerator generator,
            YazdBuildingStyle style,
            System.Random random,
            WallSegment segment,
            GameObject wall,
            Transform openingsRoot,
            bool allowDoor,
            ref bool warnedAboutBoolean)
        {
            float facadeChance = GetFacadeChance(generator, style, segment.role);
            if (facadeChance <= 0f || Next01(random) > facadeChance)
                return;

            float reservedDoorX = float.NaN;
            OpeningDefinition doorDefinition = null;

            if (allowDoor && style.doorPrefabs.Count > 0)
            {
                GameObject doorPrefab = PickOpeningPrefab(style.doorPrefabs, 0, random, out doorDefinition);
                if (doorPrefab != null && doorDefinition != null && segment.Length > doorDefinition.nominalWidth + doorDefinition.edgeClearance * 2f)
                {
                    float maxX = Mathf.Max(0f, segment.Length * 0.5f - doorDefinition.edgeClearance - doorDefinition.nominalWidth * 0.5f);
                    bool centered = Next01(random) <= style.centeredDoorChance;
                    reservedDoorX = centered ? 0f : Range(random, -maxX, maxX);

                    PlaceOpening(
                        generator,
                        wall,
                        openingsRoot,
                        doorPrefab,
                        doorDefinition,
                        reservedDoorX,
                        0f,
                        random,
                        true,
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

                float step = segment.Length / (count + 1f);
                for (int index = 0; index < count; index++)
                {
                    GameObject prefab = PickOpeningPrefab(style.windowPrefabs, floor, random, out OpeningDefinition definition);
                    if (prefab == null || definition == null)
                        continue;

                    float x = -segment.Length * 0.5f + step * (index + 1);
                    float maxJitter = step * generator.horizontalJitter;
                    x += Range(random, -maxJitter, maxJitter);

                    float halfOpening = definition.nominalWidth * 0.5f;
                    float edge = Mathf.Max(definition.edgeClearance, 0.1f);
                    if (segment.Length <= (edge + halfOpening) * 2f)
                        continue;

                    x = Mathf.Clamp(x, -segment.Length * 0.5f + edge + halfOpening, segment.Length * 0.5f - edge - halfOpening);

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
                        prefab,
                        definition,
                        x,
                        baseY,
                        random,
                        false,
                        ref warnedAboutBoolean);
                }
            }
        }

        private static float GetFacadeChance(BuildingGenerator generator, YazdBuildingStyle style, FacadeRole role)
        {
            if (role == FacadeRole.Front)
                return generator.generateFrontOpenings ? style.frontWindowChance : 0f;
            if (role == FacadeRole.Back)
                return generator.generateBackOpenings ? style.backWindowChance : 0f;
            if (role == FacadeRole.Courtyard)
                return generator.generateCourtyardOpenings ? style.courtyardWindowChance : 0f;
            return generator.generateSideOpenings ? style.sideWindowChance : 0f;
        }

        private static void PlaceOpening(
            BuildingGenerator generator,
            GameObject wall,
            Transform openingsRoot,
            GameObject prefab,
            OpeningDefinition sourceDefinition,
            float horizontal,
            float baseY,
            System.Random random,
            bool isEntrance,
            ref bool warnedAboutBoolean)
        {
            GameObject instance;
            if (PrefabUtility.IsPartOfPrefabAsset(prefab))
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, openingsRoot);
            else
                instance = Object.Instantiate(prefab, openingsRoot);

            instance.name = prefab.name;

            float yJitter = generator.floorHeight * generator.sillJitter;
            float y = Mathf.Max(0f, baseY + Range(random, -yJitter, yJitter));
            Vector3 worldPosition = wall.transform.TransformPoint(new Vector3(horizontal, y - generator.TotalHeight * 0.5f, 0f));
            instance.transform.position = worldPosition;
            instance.transform.rotation = wall.transform.rotation;

            OpeningDefinition definition = instance.GetComponent<OpeningDefinition>();
            if (definition == null)
                definition = sourceDefinition;

            if (isEntrance && generator.recessedEntrance)
            {
                OpeningDefinition instanceDefinition = instance.GetComponent<OpeningDefinition>();
                if (instanceDefinition != null && instanceDefinition.visualRoot != null)
                    instanceDefinition.visualRoot.localPosition += Vector3.back * generator.entranceRecess;
            }

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

        private static void GenerateRoof(BuildingGenerator generator, Transform roofRoot, List<MassRect> masses, List<WallSegment> segments)
        {
            YazdBuildingStyle style = generator.style;
            if (style == null || !style.generateFlatRoof)
                return;

            float y = generator.TotalHeight + style.roofThickness * 0.5f;
            Material roofMaterial = style.roofMaterial != null ? style.roofMaterial : style.wallMaterial;

            for (int i = 0; i < masses.Count; i++)
            {
                MassRect mass = masses[i];
                CreateCube(
                    "RoofSlab_" + i,
                    roofRoot,
                    new Vector3(mass.center.x, y, mass.center.y),
                    new Vector3(mass.size.x, style.roofThickness, mass.size.y),
                    roofMaterial);
            }

            if (!style.generateParapet)
                return;

            float parapetY = generator.TotalHeight + style.roofThickness + style.parapetHeight * 0.5f;
            for (int i = 0; i < segments.Count; i++)
            {
                WallSegment segment = segments[i];
                Vector2 mid = segment.Midpoint;
                Vector3 forward = new Vector3(segment.outward.x, 0f, segment.outward.y);
                GameObject parapet = CreateCube(
                    "Parapet_" + i,
                    roofRoot,
                    new Vector3(mid.x, parapetY, mid.y),
                    new Vector3(segment.Length, style.parapetHeight, style.parapetThickness),
                    style.wallMaterial);
                parapet.transform.localRotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }

        private static void GenerateIwan(BuildingGenerator generator, System.Random random, List<WallSegment> segments, Transform modulesRoot)
        {
            YazdBuildingStyle style = generator.style;
            if (!generator.generateIwan || style.iwanPrefabs.Count == 0 || Next01(random) > style.iwanChance)
                return;

            WallSegment? target = null;
            for (int i = 0; i < segments.Count; i++)
            {
                WallSegment segment = segments[i];
                if (segment.role == FacadeRole.Courtyard)
                {
                    if (!target.HasValue || segment.Length > target.Value.Length)
                        target = segment;
                }
            }

            if (!target.HasValue)
            {
                int index = FindPrimaryEntranceSegment(segments);
                if (index >= 0)
                    target = segments[index];
            }

            if (!target.HasValue)
                return;

            GameObject prefab = PickModulePrefab(style.iwanPrefabs, random);
            if (prefab == null)
                return;

            WallSegment wall = target.Value;
            Vector2 mid = wall.Midpoint;
            Vector3 outward = new Vector3(wall.outward.x, 0f, wall.outward.y);
            GameObject instance = InstantiateModule(prefab, modulesRoot);
            instance.name = prefab.name;
            instance.transform.position = generator.transform.TransformPoint(new Vector3(mid.x, 0f, mid.y));
            instance.transform.rotation = generator.transform.rotation * Quaternion.LookRotation(outward, Vector3.up);
        }

        private static void GenerateBadgirs(BuildingGenerator generator, System.Random random, List<MassRect> masses, Transform modulesRoot)
        {
            YazdBuildingStyle style = generator.style;
            if (!generator.generateBadgir || generator.badgirCount <= 0 || style.badgirPrefabs.Count == 0 || masses.Count == 0)
                return;

            int count = 0;
            for (int attempt = 0; attempt < generator.badgirCount * 3 && count < generator.badgirCount; attempt++)
            {
                if (Next01(random) > style.badgirChance)
                    continue;

                GameObject prefab = PickModulePrefab(style.badgirPrefabs, random);
                if (prefab == null)
                    continue;

                MassRect mass = masses[random.Next(0, masses.Count)];
                float inset = style.badgirRoofInset;
                float hx = Mathf.Max(0f, mass.size.x * 0.5f - inset);
                float hz = Mathf.Max(0f, mass.size.y * 0.5f - inset);
                float x = mass.center.x + Range(random, -hx, hx);
                float z = mass.center.y + Range(random, -hz, hz);
                float y = generator.TotalHeight + style.roofThickness;

                GameObject instance = InstantiateModule(prefab, modulesRoot);
                instance.name = prefab.name + "_" + count;
                instance.transform.position = generator.transform.TransformPoint(new Vector3(x, y, z));
                instance.transform.rotation = generator.transform.rotation * Quaternion.Euler(0f, random.Next(0, 4) * 90f, 0f);
                count++;
            }
        }

        private static GameObject PickModulePrefab(List<GameObject> prefabs, System.Random random)
        {
            float total = 0f;
            List<GameObject> candidates = new List<GameObject>();
            List<float> weights = new List<float>();

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

        private static GameObject InstantiateModule(GameObject prefab, Transform parent)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(prefab))
                return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            return Object.Instantiate(prefab, parent);
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
