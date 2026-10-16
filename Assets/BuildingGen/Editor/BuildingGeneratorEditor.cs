using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Emadgh.BuildingGen.Editor
{
    [CustomEditor(typeof(BuildingGenerator))]
    public sealed class BuildingGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            BuildingGenerator generator = (BuildingGenerator)target;

            EditorGUILayout.Space(8f);
            if (generator.geometryMode == BuildingGeometryMode.SolidMesh)
            {
                EditorGUILayout.HelpBox(
                    "SolidMesh: the visible/collidable building body is one procedurally generated mesh. Facade objects are metadata-only anchors used to place cutters and art; they do not render modular walls.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "LegacySegmented keeps the older per-wall prototype backend. CustomSpline is intended for SolidMesh mode.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Footprint Presets / Custom", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rectangle"))
                    SetFootprint(generator, BuildingFootprint.Rectangle);
                if (GUILayout.Button("L Shape"))
                    SetFootprint(generator, BuildingFootprint.LShape);
                if (GUILayout.Button("U Shape"))
                    SetFootprint(generator, BuildingFootprint.UShape);
                if (GUILayout.Button("Courtyard"))
                    SetFootprint(generator, BuildingFootprint.Courtyard);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Custom From Current Preset"))
                    ConvertCurrentPresetToCustom(generator);

                if (GUILayout.Button("Custom Rectangle"))
                {
                    Undo.RecordObject(generator, "Create custom rectangle footprint");
                    generator.customFootprintPoints =
                        CreateRectanglePoints(generator.width, generator.depth);
                    generator.footprint = BuildingFootprint.CustomSpline;
                    generator.geometryMode = BuildingGeometryMode.SolidMesh;
                    EditorUtility.SetDirty(generator);
                    GenerationCoordinator.Generate(generator);
                }
            }

            if (generator.footprint == BuildingFootprint.CustomSpline)
                DrawCustomFootprintControls(generator);

            if (GUILayout.Button("Traditional Yazd Courtyard"))
                ApplyTraditionalYazdPreset(generator);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);

            if (!ProBuilderBooleanBackend.IsAvailable)
            {
                EditorGUILayout.HelpBox(
                    "CSG is currently unavailable. Install ProBuilder and enable Experimental Features in Edit > Preferences/Settings > ProBuilder. The solid body and visible prefabs still generate, but door/window/iwan/facade-bay cutters will not subtract until CSG is active.",
                    MessageType.Warning);
            }

            if (generator.footprint == BuildingFootprint.Courtyard)
            {
                EditorGUILayout.HelpBox(
                    generator.geometryMode == BuildingGeometryMode.SolidMesh
                        ? "Courtyard preset generates an outer wall shell, a courtyard-facing wall shell and a roof ring inside the same MeshFilter."
                        : "Courtyard mode keeps the central court open and uses the legacy segmented wall backend.",
                    MessageType.Info);
            }
            else if (generator.footprint == BuildingFootprint.CustomSpline)
            {
                if (CustomFootprintValidationUtility.ValidateForBuild(
                        generator,
                        out string error))
                {
                    EditorGUILayout.HelpBox(
                        "CustomSpline uses the closed Scene View footprint as the first-floor/base shape. Shift+Left Click inserts a control point into the nearest edge. Local +Z is the front/street direction.",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Custom footprint is invalid: " + error,
                        MessageType.Error);
                }
            }

            if (generator.useFacadeBayPlanner)
            {
                if (generator.facadeBayCatalog == null)
                {
                    EditorGUILayout.HelpBox(
                        "Facade Bay Planner is enabled but no FacadeBayCatalog is assigned.",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        generator.geometryMode == BuildingGeometryMode.SolidMesh
                            ? "Reservation planner is active. Window / Door / Iwan / Arcade / Niche / Blank bays use logical facade anchors, while all CSG cuts target the single solid building body."
                            : "Reservation planner is active on the legacy segmented wall backend.",
                        MessageType.Info);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate"))
                    GenerationCoordinator.Generate(generator);

                if (GUILayout.Button("Randomize"))
                {
                    Undo.RecordObject(generator, "Randomize building seed");
                    generator.seed = Random.Range(int.MinValue, int.MaxValue);
                    GenerationCoordinator.Generate(generator);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("< Seed"))
                {
                    Undo.RecordObject(generator, "Previous building seed");
                    generator.DecrementSeed();
                    GenerationCoordinator.Generate(generator);
                }

                if (GUILayout.Button("Seed >"))
                {
                    Undo.RecordObject(generator, "Next building seed");
                    generator.IncrementSeed();
                    GenerationCoordinator.Generate(generator);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear Generated"))
                    GenerationCoordinator.Clear(generator);

                using (new EditorGUI.DisabledScope(generator.generatedRoot == null))
                {
                    if (GUILayout.Button("Bake Copy"))
                    {
                        GameObject baked = GenerationCoordinator.BakeCopy(generator);
                        if (baked != null)
                            Selection.activeGameObject = baked;
                    }
                }
            }
        }

        private void OnSceneGUI()
        {
            BuildingGenerator generator = (BuildingGenerator)target;
            if (generator.geometryMode != BuildingGeometryMode.SolidMesh
                || generator.footprint != BuildingFootprint.CustomSpline
                || generator.customFootprintPoints == null)
            {
                return;
            }

            DrawFrontDirection(generator);
            DrawCustomPath(generator);
            HandleCustomPoints(generator);
            HandleShiftClickInsert(generator);
        }

        private static void DrawCustomFootprintControls(
            BuildingGenerator generator)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "Custom Footprint Editing",
                EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Point"))
                    AddCustomPoint(generator);
                if (GUILayout.Button("Remove Last"))
                    RemoveLastCustomPoint(generator);
                if (GUILayout.Button("Reverse"))
                    ReverseCustomPoints(generator);
                if (GUILayout.Button("Center"))
                    CenterCustomPoints(generator);
            }

            if (GUILayout.Button("Reset Custom Rectangle"))
                ResetCustomRectangle(generator);

            EditorGUILayout.HelpBox(
                "Scene View: drag P0, P1, ... to reshape the closed first-floor footprint. Shift+Left Click inserts a point into the nearest control edge. Add Point splits the longest edge. Polyline is recommended for architectural walls; Smooth Catmull-Rom is available for organic outlines.",
                MessageType.None);
        }

        private static void DrawFrontDirection(BuildingGenerator generator)
        {
            Vector3 origin = generator.transform.position;
            float length = HandleUtility.GetHandleSize(origin) * 1.15f;

            Handles.color = new Color(0.2f, 0.8f, 1f, 1f);
            Handles.ArrowHandleCap(
                0,
                origin,
                Quaternion.LookRotation(
                    generator.transform.forward,
                    generator.transform.up),
                length,
                EventType.Repaint);

            Handles.Label(
                origin + generator.transform.forward * length,
                "Front +Z");
        }

        private static void DrawCustomPath(BuildingGenerator generator)
        {
            if (!CustomFootprintMeshBuilder.TryGetSampledLoop(
                    generator,
                    out List<Vector2> loop,
                    out string _)
                || loop.Count < 2)
            {
                DrawRawControlPolygon(generator);
                return;
            }

            Handles.color = new Color(0.95f, 0.62f, 0.12f, 1f);
            for (int i = 0; i < loop.Count; i++)
            {
                int next = (i + 1) % loop.Count;
                Vector3 a = generator.transform.TransformPoint(
                    new Vector3(loop[i].x, 0f, loop[i].y));
                Vector3 b = generator.transform.TransformPoint(
                    new Vector3(loop[next].x, 0f, loop[next].y));
                Handles.DrawAAPolyLine(4f, a, b);
            }
        }

        private static void DrawRawControlPolygon(BuildingGenerator generator)
        {
            if (generator.customFootprintPoints == null
                || generator.customFootprintPoints.Count < 2)
            {
                return;
            }

            Handles.color = Color.red;
            for (int i = 0; i < generator.customFootprintPoints.Count; i++)
            {
                int next = (i + 1) % generator.customFootprintPoints.Count;
                Vector2 pa = generator.customFootprintPoints[i];
                Vector2 pb = generator.customFootprintPoints[next];
                Handles.DrawDottedLine(
                    generator.transform.TransformPoint(
                        new Vector3(pa.x, 0f, pa.y)),
                    generator.transform.TransformPoint(
                        new Vector3(pb.x, 0f, pb.y)),
                    4f);
            }
        }

        private static void HandleCustomPoints(BuildingGenerator generator)
        {
            bool changed = false;
            for (int i = 0; i < generator.customFootprintPoints.Count; i++)
            {
                Vector2 point = generator.customFootprintPoints[i];
                Vector3 local = new Vector3(point.x, 0f, point.y);
                Vector3 world = generator.transform.TransformPoint(local);
                float size = HandleUtility.GetHandleSize(world) * 0.085f;

                Handles.color = Color.white;
                Handles.SphereHandleCap(
                    0,
                    world,
                    Quaternion.identity,
                    size,
                    EventType.Repaint);
                Handles.Label(
                    world + generator.transform.up * size * 1.5f,
                    "P" + i);

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(
                    world,
                    generator.transform.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    if (!changed)
                        Undo.RecordObject(
                            generator,
                            "Move custom footprint point");

                    Vector3 movedLocal =
                        generator.transform.InverseTransformPoint(moved);
                    generator.customFootprintPoints[i] =
                        new Vector2(movedLocal.x, movedLocal.z);
                    changed = true;
                }
            }

            if (changed)
                CustomFootprintChanged(generator);
        }

        private static void HandleShiftClickInsert(BuildingGenerator generator)
        {
            Event current = Event.current;
            if (!current.shift || current.alt)
                return;

            if (current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(
                    GUIUtility.GetControlID(FocusType.Passive));
            }

            if (current.type != EventType.MouseDown || current.button != 0)
                return;

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            Plane plane = new Plane(
                generator.transform.up,
                generator.transform.position);
            if (!plane.Raycast(ray, out float enter))
                return;

            Vector3 world = ray.GetPoint(enter);
            Vector3 local = generator.transform.InverseTransformPoint(world);
            Vector2 point = new Vector2(local.x, local.z);

            int segment = FindNearestControlSegment(
                generator.customFootprintPoints,
                point);

            Undo.RecordObject(generator, "Insert custom footprint point");
            generator.customFootprintPoints.Insert(segment + 1, point);
            CustomFootprintChanged(generator);
            current.Use();
        }

        private static int FindNearestControlSegment(
            List<Vector2> points,
            Vector2 point)
        {
            if (points == null || points.Count < 2)
                return 0;

            int best = 0;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Count];
                float distance = DistancePointToSegment(point, a, b);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        private static float DistancePointToSegment(
            Vector2 point,
            Vector2 a,
            Vector2 b)
        {
            Vector2 ab = b - a;
            if (ab.sqrMagnitude < 0.000001f)
                return Vector2.Distance(point, a);

            float t = Mathf.Clamp01(
                Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(point, a + ab * t);
        }

        private static void AddCustomPoint(BuildingGenerator generator)
        {
            List<Vector2> points = generator.customFootprintPoints;
            if (points == null)
                return;

            if (points.Count < 2)
            {
                Undo.RecordObject(generator, "Add custom footprint point");
                if (points.Count == 0)
                    points.Add(new Vector2(-2f, -2f));
                else
                    points.Add(points[0] + Vector2.right * 4f);
                CustomFootprintChanged(generator);
                return;
            }

            int longest = 0;
            float longestLength = -1f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Count];
                float length = (b - a).sqrMagnitude;
                if (length > longestLength)
                {
                    longestLength = length;
                    longest = i;
                }
            }

            Vector2 midpoint =
                (points[longest]
                 + points[(longest + 1) % points.Count]) * 0.5f;

            Undo.RecordObject(generator, "Add custom footprint point");
            points.Insert(longest + 1, midpoint);
            CustomFootprintChanged(generator);
        }

        private static void RemoveLastCustomPoint(BuildingGenerator generator)
        {
            if (generator.customFootprintPoints == null
                || generator.customFootprintPoints.Count <= 3)
            {
                return;
            }

            Undo.RecordObject(generator, "Remove custom footprint point");
            generator.customFootprintPoints.RemoveAt(
                generator.customFootprintPoints.Count - 1);
            CustomFootprintChanged(generator);
        }

        private static void ReverseCustomPoints(BuildingGenerator generator)
        {
            if (generator.customFootprintPoints == null)
                return;

            Undo.RecordObject(generator, "Reverse custom footprint");
            generator.customFootprintPoints.Reverse();
            CustomFootprintChanged(generator);
        }

        private static void CenterCustomPoints(BuildingGenerator generator)
        {
            if (generator.customFootprintPoints == null
                || generator.customFootprintPoints.Count == 0)
            {
                return;
            }

            Undo.RecordObject(generator, "Center custom footprint");
            Vector2 center = Vector2.zero;
            for (int i = 0; i < generator.customFootprintPoints.Count; i++)
                center += generator.customFootprintPoints[i];
            center /= generator.customFootprintPoints.Count;

            for (int i = 0; i < generator.customFootprintPoints.Count; i++)
                generator.customFootprintPoints[i] -= center;

            CustomFootprintChanged(generator);
        }

        private static void ResetCustomRectangle(BuildingGenerator generator)
        {
            Undo.RecordObject(generator, "Reset custom footprint rectangle");
            generator.customFootprintPoints =
                CreateRectanglePoints(generator.width, generator.depth);
            generator.customFootprintInterpolation =
                FootprintSplineInterpolation.Polyline;
            CustomFootprintChanged(generator);
        }

        private static void CustomFootprintChanged(BuildingGenerator generator)
        {
            EditorUtility.SetDirty(generator);
            if (generator.liveCustomFootprintPreview)
                GenerationCoordinator.Generate(generator);
            SceneView.RepaintAll();
        }

        private static void SetFootprint(
            BuildingGenerator generator,
            BuildingFootprint footprint)
        {
            Undo.RecordObject(generator, "Change building footprint");
            generator.footprint = footprint;

            if (footprint == BuildingFootprint.Courtyard)
            {
                generator.width = Mathf.Max(generator.width, 12f);
                generator.depth = Mathf.Max(generator.depth, 10f);
                generator.courtyardWidth =
                    Mathf.Min(
                        generator.courtyardWidth,
                        generator.width * 0.5f);
                generator.courtyardDepth =
                    Mathf.Min(
                        generator.courtyardDepth,
                        generator.depth * 0.5f);
                generator.generateCourtyardOpenings = true;
            }
            else if (footprint == BuildingFootprint.UShape)
            {
                generator.width = Mathf.Max(generator.width, 10f);
                generator.depth = Mathf.Max(generator.depth, 9f);
                generator.generateSideOpenings = true;
            }

            EditorUtility.SetDirty(generator);
            GenerationCoordinator.Generate(generator);
        }

        private static void ConvertCurrentPresetToCustom(
            BuildingGenerator generator)
        {
            Undo.RecordObject(generator, "Convert preset to custom footprint");

            if (generator.footprint != BuildingFootprint.CustomSpline)
            {
                switch (generator.footprint)
                {
                    case BuildingFootprint.LShape:
                        generator.customFootprintPoints = CreateLPoints(generator);
                        break;
                    case BuildingFootprint.UShape:
                        generator.customFootprintPoints = CreateUPoints(generator);
                        break;
                    default:
                        generator.customFootprintPoints =
                            CreateRectanglePoints(
                                generator.width,
                                generator.depth);
                        break;
                }
            }

            generator.geometryMode = BuildingGeometryMode.SolidMesh;
            generator.footprint = BuildingFootprint.CustomSpline;
            EditorUtility.SetDirty(generator);
            GenerationCoordinator.Generate(generator);
        }

        private static List<Vector2> CreateRectanglePoints(
            float width,
            float depth)
        {
            float hw = width * 0.5f;
            float hd = depth * 0.5f;
            return new List<Vector2>
            {
                new Vector2(-hw, -hd),
                new Vector2(hw, -hd),
                new Vector2(hw, hd),
                new Vector2(-hw, hd)
            };
        }

        private static List<Vector2> CreateLPoints(
            BuildingGenerator generator)
        {
            float hw = generator.width * 0.5f;
            float hd = generator.depth * 0.5f;
            float wing = Mathf.Clamp(
                generator.wingWidth,
                1.5f,
                Mathf.Min(generator.width, generator.depth) * 0.45f);

            return new List<Vector2>
            {
                new Vector2(-hw, -hd),
                new Vector2(hw, -hd),
                new Vector2(hw, -hd + wing),
                new Vector2(-hw + wing, -hd + wing),
                new Vector2(-hw + wing, hd),
                new Vector2(-hw, hd)
            };
        }

        private static List<Vector2> CreateUPoints(
            BuildingGenerator generator)
        {
            float hw = generator.width * 0.5f;
            float hd = generator.depth * 0.5f;
            float wing = Mathf.Clamp(
                generator.wingWidth,
                1.5f,
                Mathf.Min(generator.width, generator.depth) * 0.45f);
            float recess = Mathf.Clamp(
                generator.uRecessDepth,
                1.5f,
                generator.depth - generator.wallThickness * 4f);
            float innerBackZ = hd - recess;

            return new List<Vector2>
            {
                new Vector2(-hw, -hd),
                new Vector2(hw, -hd),
                new Vector2(hw, hd),
                new Vector2(hw - wing, hd),
                new Vector2(hw - wing, innerBackZ),
                new Vector2(-hw + wing, innerBackZ),
                new Vector2(-hw + wing, hd),
                new Vector2(-hw, hd)
            };
        }

        private static void ApplyTraditionalYazdPreset(
            BuildingGenerator generator)
        {
            Undo.RecordObject(generator, "Apply traditional Yazd preset");
            generator.geometryMode = BuildingGeometryMode.SolidMesh;
            generator.footprint = BuildingFootprint.Courtyard;
            generator.width = Mathf.Max(generator.width, 13f);
            generator.depth = Mathf.Max(generator.depth, 11f);
            generator.courtyardWidth =
                Mathf.Clamp(generator.width * 0.43f, 4.5f, 7f);
            generator.courtyardDepth =
                Mathf.Clamp(generator.depth * 0.43f, 4f, 6.5f);
            generator.floors = Mathf.Max(generator.floors, 2);
            generator.wallThickness =
                Mathf.Max(generator.wallThickness, 0.36f);
            generator.generateCourtyardOpenings = true;
            generator.generateCourtyardArcade = true;
            generator.generateHashtiAndCorridor = true;
            generator.recessedEntrance = true;
            generator.generateIwan = true;
            generator.cutIwanRecess = true;
            generator.generateBadgir = true;
            generator.badgirCount =
                Mathf.Max(1, generator.badgirCount);
            generator.generateRoofForms = true;
            generator.maxRoofForms =
                Mathf.Max(1, generator.maxRoofForms);

            EditorUtility.SetDirty(generator);
            GenerationCoordinator.Generate(generator);
        }
    }
}
