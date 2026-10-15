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
                "Scene editing: drag point handles. Shift + left-click appends a point. Gate markers use distance along the spline and stay meaningful when the path changes.",
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
            DrawOpeningGuides(spline);
            HandlePoints(spline);
            HandleShiftClickAppend(spline);
        }

        private static void DrawPath(WallSpline spline)
        {
            List<Vector3> sampled = WallSplineBuilder.GetSampledPath(spline);
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

        private static void DrawOpeningGuides(WallSpline spline)
        {
            if (!spline.showOpeningGuides || spline.openings == null)
                return;

            for (int i = 0; i < spline.openings.Count; i++)
            {
                WallSplineOpening opening = spline.openings[i];
                if (opening == null || !opening.enabled)
                    continue;

                Vector3 localPosition;
                Vector3 localTangent;
                if (!WallSplineBuilder.EvaluateSplineAtDistance(spline, opening.distance, out localPosition, out localTangent))
                    continue;

                Vector3 worldPosition = spline.transform.TransformPoint(localPosition);
                Vector3 worldTangent = spline.transform.TransformDirection(localTangent).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, worldTangent).normalized;
                float half = opening.width * 0.5f;

                Handles.color = new Color(0.95f, 0.2f, 0.15f, 1f);
                Handles.DrawAAPolyLine(5f, worldPosition - worldTangent * half, worldPosition + worldTangent * half);
                Handles.DrawLine(worldPosition, worldPosition + Vector3.up * 2.2f);
                Handles.Label(worldPosition + Vector3.up * 2.35f + side * 0.1f, "Gate " + i + "  " + opening.width.ToString("0.00") + "m");
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
            EditorUtility.SetDirty(spline);
            WallSplineBuilder.Generate(spline);
            current.Use();
        }

        private static void AddGateMarker(WallSpline spline)
        {
            Undo.RecordObject(spline, "Add wall gate marker");
            float length = WallSplineBuilder.GetPathLength(spline);
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
            EditorUtility.SetDirty(spline);
            WallSplineBuilder.Generate(spline);
        }

        private static void RemoveLastPoint(WallSpline spline)
        {
            if (spline.controlPoints.Count <= 2)
                return;
            Undo.RecordObject(spline, "Remove wall spline point");
            spline.controlPoints.RemoveAt(spline.controlPoints.Count - 1);
            EditorUtility.SetDirty(spline);
            WallSplineBuilder.Generate(spline);
        }

        private static void ReversePoints(WallSpline spline)
        {
            float length = WallSplineBuilder.GetPathLength(spline);
            Undo.RecordObject(spline, "Reverse wall spline");
            spline.controlPoints.Reverse();

            if (spline.openings != null)
            {
                for (int i = 0; i < spline.openings.Count; i++)
                {
                    WallSplineOpening opening = spline.openings[i];
                    if (opening != null)
                        opening.distance = Mathf.Max(0f, length - opening.distance);
                }
            }

            EditorUtility.SetDirty(spline);
            WallSplineBuilder.Generate(spline);
        }
    }
}
