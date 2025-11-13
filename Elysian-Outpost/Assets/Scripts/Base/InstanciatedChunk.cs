using Libs.VoxelMeshOptimizer.Toolkit;
using ScriptableObjectsDefinition;
using UnityEngine;

namespace Base
{
    public class InstanciatedChunk : MonoBehaviour
    {
            private MeshFilter meshFilter;
            private MeshCollider meshCollider;

            private void Awake()
            {
                meshFilter = gameObject.GetComponent<MeshFilter>();
                meshCollider = gameObject.GetComponent<MeshCollider>();
            }
        

            public void SetMesh(Mesh mesh, TextureAtlas atlas)
            {
                meshFilter.mesh = ObjExporter.ToUnityMesh(mesh, atlas);
                meshCollider.sharedMesh = meshFilter.mesh;
            }

    }
}