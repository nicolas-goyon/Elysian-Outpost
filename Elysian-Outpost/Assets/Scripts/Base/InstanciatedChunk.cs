using Libs.VoxelMeshOptimizer.Toolkit;
using ScriptableObjectsDefinition;
using UnityEngine;

namespace Base
{
    public class InstanciatedChunk : MonoBehaviour
    {
            private MeshFilter meshFilter;
            private MeshCollider meshCollider;
            public ExampleChunk chunk { get; set; }

            private void Awake()
            {
                meshFilter = gameObject.GetComponent<MeshFilter>();
                meshCollider = gameObject.GetComponent<MeshCollider>();
            }
        

            public void SetMesh(ExampleMesh exampleMesh, TextureAtlas atlas)
            {
                meshFilter.mesh = ObjExporter.ToUnityMesh(exampleMesh, atlas);
                meshCollider.sharedMesh = meshFilter.mesh;
            }

    }
}