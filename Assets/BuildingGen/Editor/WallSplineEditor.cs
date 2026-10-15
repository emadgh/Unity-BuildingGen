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
                "Scene editing: drag the point handles. Hold Shift and left-click on the ground plane to append a new point. The path is stored in local space.",
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
            HandlePoints(spline);
            HandleShiftClickAppend(spline);
        }

        private static void DrawPath(WallSpline spline)
        {
            List<Vector3> sampled = WallSplineBuilder.GetSampledPath(spline);
            if (sampled.Count < 2)
                return;

            Handles.color = new Color(0.95f, 0.65f, 0.12f, 1f);
            for (int i = 0; i < sampled.Count - 1; i++)
            {
                Vector3 a = spline.transform.TransformPoint(sampled[i]);
                Vector3 b = spline.transform.TransformPoint(sampled[i + 1]);
                Handles.DrawAAPolyLine(4f, a, b);
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
            Undo.RecordObject(spline, "Reverse wall spline");
            spline.controlPoints.Reverse();
            EditorUtility.SetDirty(spline);
            WallSplineBuilder.Generate(spline);
        }
    }
}
