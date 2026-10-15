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
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Yazd Footprint Presets", EditorStyles.boldLabel);

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

            if (GUILayout.Button("Traditional Yazd Courtyard"))
                ApplyTraditionalYazdPreset(generator);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);

            if (!ProBuilderBooleanBackend.IsAvailable)
            {
                EditorGUILayout.HelpBox(
                    "CSG is currently unavailable. Install ProBuilder and enable Experimental Features in Edit > Preferences/Settings > ProBuilder. Prefabs/modules are still placed, but door/window/iwan cutters will not modify walls until CSG is active.",
                    MessageType.Warning);
            }

            if (generator.footprint == BuildingFootprint.Courtyard)
            {
                EditorGUILayout.HelpBox(
                    "Courtyard mode keeps the central court open. v0.3 can add courtyard-facing openings, repeated arcade bays, a recessed iwan, hashti/bent entrance route, badgir and roof-form modules.",
                    MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate"))
                    BuildingBuilder.Generate(generator);

                if (GUILayout.Button("Randomize"))
                {
                    Undo.RecordObject(generator, "Randomize building seed");
                    generator.seed = Random.Range(int.MinValue, int.MaxValue);
                    BuildingBuilder.Generate(generator);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("< Seed"))
                {
                    Undo.RecordObject(generator, "Previous building seed");
                    generator.DecrementSeed();
                    BuildingBuilder.Generate(generator);
                }

                if (GUILayout.Button("Seed >"))
                {
                    Undo.RecordObject(generator, "Next building seed");
                    generator.IncrementSeed();
                    BuildingBuilder.Generate(generator);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear Generated"))
                    BuildingBuilder.Clear(generator);

                using (new EditorGUI.DisabledScope(generator.generatedRoot == null))
                {
                    if (GUILayout.Button("Bake Copy"))
                    {
                        GameObject baked = BuildingBuilder.BakeCopy(generator);
                        if (baked != null)
                            Selection.activeGameObject = baked;
                    }
                }
            }
        }

        private static void SetFootprint(BuildingGenerator generator, BuildingFootprint footprint)
        {
            Undo.RecordObject(generator, "Change building footprint");
            generator.footprint = footprint;

            if (footprint == BuildingFootprint.Courtyard)
            {
                generator.width = Mathf.Max(generator.width, 12f);
                generator.depth = Mathf.Max(generator.depth, 10f);
                generator.courtyardWidth = Mathf.Min(generator.courtyardWidth, generator.width * 0.5f);
                generator.courtyardDepth = Mathf.Min(generator.courtyardDepth, generator.depth * 0.5f);
                generator.generateCourtyardOpenings = true;
            }
            else if (footprint == BuildingFootprint.UShape)
            {
                generator.width = Mathf.Max(generator.width, 10f);
                generator.depth = Mathf.Max(generator.depth, 9f);
                generator.generateSideOpenings = true;
            }

            EditorUtility.SetDirty(generator);
            BuildingBuilder.Generate(generator);
        }

        private static void ApplyTraditionalYazdPreset(BuildingGenerator generator)
        {
            Undo.RecordObject(generator, "Apply traditional Yazd preset");
            generator.footprint = BuildingFootprint.Courtyard;
            generator.width = Mathf.Max(generator.width, 13f);
            generator.depth = Mathf.Max(generator.depth, 11f);
            generator.courtyardWidth = Mathf.Clamp(generator.width * 0.43f, 4.5f, 7f);
            generator.courtyardDepth = Mathf.Clamp(generator.depth * 0.43f, 4f, 6.5f);
            generator.floors = Mathf.Max(generator.floors, 2);
            generator.wallThickness = Mathf.Max(generator.wallThickness, 0.36f);
            generator.generateCourtyardOpenings = true;
            generator.generateCourtyardArcade = true;
            generator.generateHashtiAndCorridor = true;
            generator.recessedEntrance = true;
            generator.generateIwan = true;
            generator.cutIwanRecess = true;
            generator.generateBadgir = true;
            generator.badgirCount = Mathf.Max(1, generator.badgirCount);
            generator.generateRoofForms = true;
            generator.maxRoofForms = Mathf.Max(1, generator.maxRoofForms);

            EditorUtility.SetDirty(generator);
            BuildingBuilder.Generate(generator);
        }
    }
}
