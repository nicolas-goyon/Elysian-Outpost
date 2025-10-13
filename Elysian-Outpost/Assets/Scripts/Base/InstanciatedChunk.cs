using Libs.VoxelMeshOptimizer.Toolkit;
using UnityEngine;

namespace Base
{
    public class InstanciatedChunk : MonoBehaviour
    {
            private MeshFilter meshFilter;

            private void Awake()
            {
                meshFilter = gameObject.GetComponent<MeshFilter>();
            }
        

            public void SetMesh(ExampleMesh exampleMesh)
            {
                meshFilter.mesh = ObjExporter.ToUnityMesh(exampleMesh);
            }

    }
}