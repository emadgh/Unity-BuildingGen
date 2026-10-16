using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Emadgh.BuildingGen.Editor
{
    internal static class CustomFootprintSampleFactory
    {
        [MenuItem("Tools/BuildingGen/Create Custom Footprint Demo")]
        public static void CreateCustomFootprintDemo()
        {
            SampleFactory.CreateSimpleYazdDemo();

            GameObject go = GameObject.Find("YazdBuilding_Demo");
            if (go == null)
            {
                Debug.LogError("BuildingGen custom footprint demo: YazdBuilding_Demo was not created.");
                return;
            }

            BuildingGenerator generator = go.GetComponent<BuildingGenerator>();
            if (generator == null)
            {
                Debug.LogError("BuildingGen custom footprint demo: BuildingGenerator is missing.", go);
                return;
            }

            generator.geometryMode = BuildingGeometryMode.SolidMesh;
            generator.footprint = BuildingFootprint.CustomSpline;
            generator.customFootprintInterpolation = FootprintSplineInterpolation.Polyline;
            generator.customFootprintSubdivisions = 5;
            generator.liveCustomFootprintPreview = true;
            generator.generateMeshCollider = true;
            generator.generateHashtiAndCorridor = false;
            generator.customFootprintPoints = new List<Vector2>
            {
                new Vector2(-6.2f, -4.7f),
                new Vector2(1.0f, -4.7f),
                new Vector2(1.0f, -3.0f),
                new Vector2(5.8f, -3.0f),
                new Vector2(5.8f, 1.6f),
                new Vector2(3.6f, 1.6f),
                new Vector2(3.6f, 4.2f),
                new Vector2(-2.4f, 4.2f),
                new Vector2(-2.4f, 2.8f),
                new Vector2(-6.2f, 2.8f)
            };

            GenerationCoordinator.Generate(generator);
            EditorUtility.SetDirty(generator);
            Selection.activeGameObject = go;
            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log(
                "BuildingGen v0.7: custom first-floor footprint demo created. Select YazdBuilding_Demo and drag P0...Pn in Scene View; Shift+Left Click appends points.",
                go);
        }
    }
}
