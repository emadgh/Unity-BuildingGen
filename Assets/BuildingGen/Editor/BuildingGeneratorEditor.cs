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
                    generator.customFootprintPoints = CreateRectanglePoints(generator.width, generator.depth);
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
                if (CustomFootprintMeshBuilder.ValidateControlPolygon(generator, out string error))
                {
                    EditorGUILayout.HelpBox(
                        "CustomSpline uses the closed Scene View footprint as the first-floor/base shape, then generates the complete house body as one solid mesh. Shift+Left Click appends a point.",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("Custom footprint is invalid: " + error, MessageType.Error);
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

            DrawCustomPath(generator);
            HandleCustomPoints(generator);
            HandleShiftClickAppend(generator);
        }

        private static void DrawCustomFootprintControls(BuildingGenerator generator)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Custom Footprint Editing", EditorStyles.boldLabel);

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

            EditorGUILayout.HelpBox(
                "Scene View: drag P0, P1, ... to reshape the closed footprint. Hold Shift and left-click on the generator ground plane to append a point. Polyline is recommended for architectural walls; Smooth Catmull-Rom is available for organic footprints.",
                MessageType.None);
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
                    generator.transform.TransformPoint(new Vector3(pa.x, 0f, pa.y)),
                    generator.transform.TransformPoint(new Vector3(pb.x, 0f, pb.y)),
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
                Handles.SphereHandleCap(0, world, Quaternion.identity, size, EventType.Repaint);
                Handles.Label(world + generator.transform.up * size * 1.5f, "P" + i);

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(world, generator.transform.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    if (!changed)
                        Undo.RecordObject(generator, "Move custom footprint point");

                    Vector3 movedLocal = generator.transform.InverseTransformPoint(moved);
                    generator.customFootprintPoints[i] =
                        new Vector2(movedLocal.x, movedLocal.z);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(generator);
                if (generator.liveCustomFootprintPreview)
                    GenerationCoordinator.Generate(generator);
            }
        }

        private static void HandleShiftClickAppend(BuildingGenerator generator)
        {
            Event current = Event.current;
            if (!current.shift || current.alt)
                return;

            if (current.type == EventType.Layout)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            if (current.type != EventType.MouseDown || current.button != 0)
                return;

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            Plane plane = new Plane(generator.transform.up, generator.transform.position);
            if (!plane.Raycast(ray, out float enter))
                return;

            Vector3 world = ray.GetPoint(enter);
            Vector3 local = generator.transform.InverseTransformPoint(world);
            Undo.RecordObject(generator, "Append custom footprint point");
            generator.customFootprintPoints.Add(new Vector2(local.x, local.z));
            EditorUtility.SetDirty(generator);

            if (generator.liveCustomFootprintPreview)
                GenerationCoordinator.Generate(generator);
            current.Use();
        }

        private static void AddCustomPoint(BuildingGenerator generator)
        {
            Undo.RecordObject(generator, "Add custom footprint point");
            List<Vector2> points = generator.customFootprintPoints;
            if (points.Count == 0)
            {
                points.Add(new Vector2(-2f, -2f));
            }
            else if (points.Count == 1)
            {
                points.Add(points[0] + Vector2.right * 4f);
            }
            else
            {
                Vector2 last = points[points.Count - 1];
                Vector2 previous = points[points.Count - 2];
                Vector2 direction = last - previous;
                if (direction.sqrMagnitude < 0.01f)
                    direction = Vector2.right;
                points.Add(last + direction.normalized * 3f);
            }

            EditorUtility.SetDirty(generator);
            if (generator.liveCustomFootprintPreview)
                GenerationCoordinator.Generate(generator);
        }

        private static void RemoveLastCustomPoint(BuildingGenerator generator)
        {
            if (generator.customFootprintPoints.Count <= 3)
                return;

            Undo.RecordObject(generator, "Remove custom footprint point");
            generator.customFootprintPoints.RemoveAt(
                generator.customFootprintPoints.Count - 1);
            EditorUtility.SetDirty(generator);
            if (generator.liveCustomFootprintPreview)
                GenerationCoordinator.Generate(generator);
        }

        private static void ReverseCustomPoints(BuildingGenerator generator)
        {
            Undo.RecordObject(generator, "Reverse custom footprint");
            generator.customFootprintPoints.Reverse();
            EditorUtility.SetDirty(generator);
            if (generator.liveCustomFootprintPreview)
                GenerationCoordinator.Generate(generator);
        }

        private static void CenterCustomPoints(BuildingGenerator generator)
        {
            if (generator.customFootprintPoints.Count == 0)
                return;

            Undo.RecordObject(generator, "Center custom footprint");
            Vector2 center = Vector2.zero;
            for (int i = 0; i < generator.customFootprintPoints.Count; i++)
                center += generator.customFootprintPoints[i];
            center /= generator.customFootprintPoints.Count;

            for (int i = 0; i < generator.customFootprintPoints.Count; i++)
                generator.customFootprintPoints[i] -= center;

            EditorUtility.SetDirty(generator);
            if (generator.liveCustomFootprintPreview)
                GenerationCoordinator.Generate(generator);
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
                    Mathf.Min(generator.courtyardWidth, generator.width * 0.5f);
                generator.courtyardDepth =
                    Mathf.Min(generator.courtyardDepth, generator.depth * 0.5f);
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

        private static void ConvertCurrentPresetToCustom(BuildingGenerator generator)
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
                            CreateRectanglePoints(generator.width, generator.depth);
                        break;
                }
            }

            generator.geometryMode = BuildingGeometryMode.SolidMesh;
            generator.footprint = BuildingFootprint.CustomSpline;
            EditorUtility.SetDirty(generator);
            GenerationCoordinator.Generate(generator);
        }

        private static List<Vector2> CreateRectanglePoints(float width, float depth)
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

        private static List<Vector2> CreateLPoints(BuildingGenerator generator)
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

        private static List<Vector2> CreateUPoints(BuildingGenerator generator)
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

        private static void ApplyTraditionalYazdPreset(BuildingGenerator generator)
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
