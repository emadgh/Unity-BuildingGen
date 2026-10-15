using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Emadgh.BuildingGen.Editor
{
    [CustomEditor(typeof(WallSpline))]
    public sealed class WallSplineEditor : UnityEditor.Editor
    {
        [MenuItem("Tools/BuildingGen/Create Wall Spline")]
        public static void CreateWallSpline()
        {
            GameObject go = new GameObject("WallSpline");
            Undo.RegisterCreatedObjectUndo(go, "Create wall spline");
            WallSpline spline = go.AddComponent<WallSpline>();
            Selection.activeGameObject = go;
            SceneView.lastActiveSceneView?.FrameSelected();
            EditorUtility.SetDirty(spline);
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            WallSpline spline = (WallSpline)target;

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Scene editing: drag point handles. Shift + left-click appends a point. Gate markers use distance along the spline. In SceneView drag the red gate center handle along the wall and the yellow handle to change gate width.",
                MessageType.Info);

            EditorGUILayout.LabelField("Spline Editing", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Point"))
                    AddPoint(spline);
                if (GUILayout.Button("Remove Last"))
                    RemoveLastPoint(spline);
                if (GUILayout.Button("Reverse"))
                    ReversePoints(spline);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Gate Marker"))
                    AddGateMarker(spline);

                if (GUILayout.Button("Conform + Generate"))
                {
                    Undo.RecordObject(spline, "Enable wall terrain conform");
                    spline.conformToTerrain = true;
                    EditorUtility.SetDirty(spline);
                    WallSplineBuilder.Generate(spline);
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate"))
                    WallSplineBuilder.Generate(spline);
                if (GUILayout.Button("Randomize"))
                {
                    Undo.RecordObject(spline, "Randomize wall spline seed");
                    spline.seed = Random.Range(int.MinValue, int.MaxValue);
                    WallSplineBuilder.Generate(spline);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Seed >"))
                {
                    Undo.RecordObject(spline, "Next wall spline seed");
                    spline.IncrementSeed();
                    WallSplineBuilder.Generate(spline);
                }

                if (GUILayout.Button("Clear Generated"))
                    WallSplineBuilder.Clear(spline);
            }

            using (new EditorGUI.DisabledScope(spline.generatedRoot == null))
            {
                if (GUILayout.Button("Bake Wall Copy"))
                {
                    GameObject baked = WallSplineBuilder.BakeCopy(spline);
                    if (baked != null)
                        Selection.activeGameObject = baked;
                }
            }
        }

        private void OnSceneGUI()
        {
            WallSpline spline = (WallSpline)target;
            if (spline.controlPoints == null || spline.controlPoints.Count == 0)
                return;

            DrawPath(spline);
            HandleOpeningGuides(spline);
            HandlePoints(spline);
            HandleShiftClickAppend(spline);
        }

        private static void DrawPath(WallSpline spline)
        {
            List<Vector3> sampled = WallSplineEditorPathUtility.GetSampledPath(spline);
            if (sampled.Count < 2)
                return;

            Handles.color = spline.conformToTerrain
                ? new Color(0.35f, 0.85f, 0.45f, 1f)
                : new Color(0.95f, 0.65f, 0.12f, 1f);

            for (int i = 0; i < sampled.Count - 1; i++)
            {
                Vector3 a = spline.transform.TransformPoint(sampled[i]);
                Vector3 b = spline.transform.TransformPoint(sampled[i + 1]);
                Handles.DrawAAPolyLine(4f, a, b);
            }
        }

        private static void HandleOpeningGuides(WallSpline spline)
        {
            if (!spline.showOpeningGuides || spline.openings == null || spline.openings.Count == 0)
                return;

            List<Vector3> path = WallSplineEditorPathUtility.GetSampledPath(spline);
            float totalLength = CalculateLength(path);
            if (totalLength < 0.001f)
                return;

            bool changed = false;

            for (int i = 0; i < spline.openings.Count; i++)
            {
                WallSplineOpening opening = spline.openings[i];
                if (opening == null || !opening.enabled)
                    continue;

                opening.distance = Mathf.Clamp(opening.distance, 0f, totalLength);

                Vector3 localPosition;
                Vector3 localTangent;
                if (!EvaluatePathAtDistance(path, opening.distance, out localPosition, out localTangent))
                    continue;

                Vector3 worldPosition = spline.transform.TransformPoint(localPosition);
                Vector3 worldTangent = spline.transform.TransformDirection(localTangent).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, worldTangent).normalized;
                float half = opening.width * 0.5f;
                float handleSize = HandleUtility.GetHandleSize(worldPosition) * 0.10f;

                Handles.color = new Color(0.95f, 0.2f, 0.15f, 1f);
                Handles.DrawAAPolyLine(5f, worldPosition - worldTangent * half, worldPosition + worldTangent * half);
                Handles.DrawLine(worldPosition, worldPosition + Vector3.up * 2.2f);
                Handles.Label(
                    worldPosition + Vector3.up * 2.35f + side * 0.1f,
                    "Gate " + i + "  D:" + opening.distance.ToString("0.00") + "m  W:" + opening.width.ToString("0.00") + "m");

                EditorGUI.BeginChangeCheck();
                Handles.color = new Color(0.95f, 0.2f, 0.15f, 1f);
                Vector3 movedCenter = Handles.Slider(
                    worldPosition,
                    worldTangent,
                    handleSize,
                    Handles.SphereHandleCap,
                    0f);
                if (EditorGUI.EndChangeCheck())
                {
                    if (!changed)
                        Undo.RecordObject(spline, "Move wall gate");

                    float worldDelta = Vector3.Dot(movedCenter - worldPosition, worldTangent);
                    float localScale = spline.transform.TransformVector(localTangent).magnitude;
                    if (localScale < 0.0001f)
                        localScale = 1f;

                    opening.distance = Mathf.Clamp(opening.distance + worldDelta / localScale, 0f, totalLength);
                    changed = true;
                }

                EditorGUI.BeginChangeCheck();
                Handles.color = new Color(1f, 0.78f, 0.1f, 1f);
                Vector3 widthHandlePosition = worldPosition + worldTangent * half;
                Vector3 movedWidthHandle = Handles.Slider(
                    widthHandlePosition,
                    worldTangent,
                    handleSize * 0.85f,
                    Handles.CubeHandleCap,
                    0f);
                if (EditorGUI.EndChangeCheck())
                {
                    if (!changed)
                        Undo.RecordObject(spline, "Resize wall gate");

                    float projectedHalfWidth = Mathf.Abs(Vector3.Dot(movedWidthHandle - worldPosition, worldTangent));
                    opening.width = Mathf.Max(0.3f, projectedHalfWidth * 2f);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(spline);
                WallSplineBuilder.Generate(spline);
            }
        }

        private static void HandlePoints(WallSpline spline)
        {
            bool changed = false;
            for (int i = 0; i < spline.controlPoints.Count; i++)
            {
                Vector3 world = spline.transform.TransformPoint(spline.controlPoints[i]);
                float size = HandleUtility.GetHandleSize(world) * 0.09f;

                Handles.color = Color.white;
                Handles.SphereHandleCap(0, world, Quaternion.identity, size, EventType.Repaint);
                Handles.Label(world + Vector3.up * size * 1.5f, "P" + i);

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(world, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    if (!changed)
                        Undo.RecordObject(spline, "Move wall spline point");
                    spline.controlPoints[i] = spline.transform.InverseTransformPoint(moved);
                    changed = true;
                }
            }

            if (changed)
            {
                ClampOpeningDistances(spline);
                EditorUtility.SetDirty(spline);
                WallSplineBuilder.Generate(spline);
            }
        }

        private static void HandleShiftClickAppend(WallSpline spline)
        {
            Event current = Event.current;
            if (!current.shift || current.alt)
                return;

            if (current.type == EventType.Layout)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            if (current.type != EventType.MouseDown || current.button != 0)
                return;

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            Plane plane = new Plane(spline.transform.up, spline.transform.position);
            float enter;
            if (!plane.Raycast(ray, out enter))
                return;

            Vector3 world = ray.GetPoint(enter);
            Undo.RecordObject(spline, "Append wall spline point");
            spline.controlPoints.Add(spline.transform.InverseTransformPoint(world));
            ClampOpeningDistances(spline);
            EditorUtility.SetDirty(spline);
            WallSplineBuilder.Generate(spline);
            current.Use();
        }

        private static void AddGateMarker(WallSpline spline)
        {
            Undo.RecordObject(spline, "Add wall gate marker");
            float length = WallSplineEditorPathUtility.GetPathLength(spline);
            WallSplineOpening opening = new WallSplineOpening
            {
                enabled = true,
                distance = length * 0.5f,
                width = 1.8f
            };
            spline.openings.Add(opening);
            EditorUtility.SetDirty(spline);
            WallSplineBuilder.Generate(spline);
        }

        private static void AddPoint(WallSpline spline)
        {
            Undo.RecordObject(spline, "Add wall spline point");
            if (spline.controlPoints.Count == 0)
            {
                spline.controlPoints.Add(Vector3.zero);
            }
            else if (spline.controlPoints.Count == 1)
            {
                spline.controlPoints.Add(spline.controlPoints[0] + Vector3.right * 3f);
            }
            else
            {
                Vector3 last = spline.controlPoints[spline.controlPoints.Count - 1];
                Vector3 previous = spline.controlPoints[spline.controlPoints.Count - 2];
                Vector3 direction = last - previous;
                if (direction.sqrMagnitude < 0.01f)
                    direction = Vector3.right * 3f;
                spline.controlPoints.Add(last + direction.normalized * 3f);
            }
            ClampOpeningDistances(spline);
            EditorUtility.SetDirty(spline);
            WallSplineBuilder.Generate(spline);
        }

        private static void RemoveLastPoint(WallSpline spline)
        {
            if (spline.controlPoints.Count <= 2)
                return;
            Undo.RecordObject(spline, "Remove wall spline point");
            spline.controlPoints.RemoveAt(spline.controlPoints.Count - 1);
            ClampOpeningDistances(spline);
            EditorUtility.SetDirty(spline);
            WallSplineBuilder.Generate(spline);
        }

        private static void ReversePoints(WallSpline spline)
        {
            float length = WallSplineEditorPathUtility.GetPathLength(spline);
            Undo.RecordObject(spline, "Reverse wall spline");
            spline.controlPoints.Reverse();

            if (spline.openings != null)
            {
                for (int i = 0; i < spline.openings.Count; i++)
                {
                    WallSplineOpening opening = spline.openings[i];
                    if (opening != null)
                        opening.distance = Mathf.Clamp(length - opening.distance, 0f, length);
                }
            }

            EditorUtility.SetDirty(spline);
            WallSplineBuilder.Generate(spline);
        }

        private static void ClampOpeningDistances(WallSpline spline)
        {
            if (spline.openings == null || spline.openings.Count == 0)
                return;

            float length = WallSplineEditorPathUtility.GetPathLength(spline);
            for (int i = 0; i < spline.openings.Count; i++)
            {
                WallSplineOpening opening = spline.openings[i];
                if (opening != null)
                    opening.distance = Mathf.Clamp(opening.distance, 0f, length);
            }
        }

        private static float CalculateLength(List<Vector3> path)
        {
            float total = 0f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 delta = path[i + 1] - path[i];
                delta.y = 0f;
                total += delta.magnitude;
            }
            return total;
        }

        private static bool EvaluatePathAtDistance(
            List<Vector3> path,
            float targetDistance,
            out Vector3 position,
            out Vector3 tangent)
        {
            targetDistance = Mathf.Max(0f, targetDistance);
            float cursor = 0f;
            Vector3 lastTangent = Vector3.forward;

            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 a = path[i];
                Vector3 b = path[i + 1];
                Vector3 planar = b - a;
                planar.y = 0f;
                float length = planar.magnitude;
                if (length < 0.001f)
                    continue;

                lastTangent = planar.normalized;
                if (cursor + length >= targetDistance)
                {
                    float t = Mathf.Clamp01((targetDistance - cursor) / length);
                    position = Vector3.Lerp(a, b, t);
                    tangent = lastTangent;
                    return true;
                }

                cursor += length;
            }

            if (path.Count > 0)
            {
                position = path[path.Count - 1];
                tangent = lastTangent;
                return true;
            }

            position = Vector3.zero;
            tangent = Vector3.forward;
            return false;
        }
    }
}
