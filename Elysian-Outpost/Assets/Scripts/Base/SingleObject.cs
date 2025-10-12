using System.IO;
using Libs.VoxelMeshOptimizer.Toolkit;
using UnityEngine;
using Mesh = Libs.VoxelMeshOptimizer.Mesh;

namespace Base
{
    public class SingleObject : MonoBehaviour
    {
        [SerializeField] private GameObject _templateObject;

        
        private void Start()
        {
            // Initialize a default mesh here.
            ExampleChunk exampleChunk = new ExampleChunk(PerlinNoiseChunkGen.CreatePerlinLandscape(50, 123));

            ExampleMesh baseMesh = exampleChunk.ToMesh();
            string filePath = Path.Combine("./", "ChunkBase" + ".obj");
            File.WriteAllText(filePath, ObjExporter.MeshToObjString(baseMesh));
            Create(baseMesh, transform.position + Vector3.right * 2);
        }

        private InstanciatedChunk Create(ExampleMesh mesh, Vector3 position)
        {
            GameObject obj = Instantiate(_templateObject);
            InstanciatedChunk singleObject = obj.GetComponent<InstanciatedChunk>();
            obj.transform.position = position;
            
            singleObject.SetMesh(mesh);
            
            return singleObject;
        }
    }
}
