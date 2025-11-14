using Libs.VoxelMeshOptimizer.Toolkit;
using ScriptableObjectsDefinition;
using Unity.AI.Navigation;
using UnityEngine;
using Mesh = Libs.VoxelMeshOptimizer.Mesh;

namespace Base.Terrain
{
    public class InstanciatedChunk : MonoBehaviour
    {
        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;
        private NavMeshSurface _navMeshSurface;

        private void Awake()
        {
            _meshFilter = gameObject.GetComponent<MeshFilter>();
            _meshCollider = gameObject.GetComponent<MeshCollider>();
            _navMeshSurface = gameObject.GetComponent<NavMeshSurface>();
        }


        public void SetMesh(Mesh mesh, TextureAtlas atlas)
        {
            _meshFilter.mesh = ObjExporter.ToUnityMesh(mesh, atlas);
            _meshCollider.sharedMesh = _meshFilter.mesh;
            _navMeshSurface.BuildNavMesh();
        }
    }
}