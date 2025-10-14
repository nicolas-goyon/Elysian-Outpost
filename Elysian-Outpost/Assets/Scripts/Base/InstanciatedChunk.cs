using Libs.VoxelMeshOptimizer.Toolkit;
using ScriptableObjectsDefinition;
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
        

            public void SetMesh(ExampleMesh exampleMesh, TextureAtlas atlas)
            {
                meshFilter.mesh = ObjExporter.ToUnityMesh(exampleMesh, atlas);
            }

    }
}