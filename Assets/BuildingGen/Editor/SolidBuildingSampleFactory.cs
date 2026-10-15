using UnityEditor;
using UnityEngine;

namespace Emadgh.BuildingGen.Editor
{
    internal static class SolidBuildingSampleFactory
    {
        [MenuItem("Tools/BuildingGen/Create Solid Yazd Demo")]
        public static void CreateSolidYazdDemo()
        {
            // Reuse the existing primitive asset factory, then regenerate the scene object
            // through the new v0.6 one-mesh backend.
            SampleFactory.CreateSimpleYazdDemo();

            GameObject go = GameObject.Find("YazdBuilding_Demo");
            if (go == null)
            {
                Debug.LogError("BuildingGen solid demo: YazdBuilding_Demo was not created.");
                return;
            }

            BuildingGenerator generator = go.GetComponent<BuildingGenerator>();
            if (generator == null)
            {
                Debug.LogError("BuildingGen solid demo: BuildingGenerator is missing.", go);
                return;
            }

            generator.geometryMode = BuildingGeometryMode.SolidMesh;
            generator.generateMeshCollider = true;
            generator.useFacadeBayPlanner = false;
            generator.generateHashtiAndCorridor = false;

            GenerationCoordinator.Generate(generator);
            EditorUtility.SetDirty(generator);
            Selection.activeGameObject = go;

            Debug.Log(
                "BuildingGen v0.6: solid Yazd demo created. The building body is one generated MeshFilter; door/window art remains prefab-based and cutters target that mesh.",
                go);
        }
    }
}
