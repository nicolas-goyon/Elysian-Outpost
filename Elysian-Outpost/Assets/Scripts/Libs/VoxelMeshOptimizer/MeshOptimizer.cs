namespace Libs.VoxelMeshOptimizer
{

    public interface MeshOptimizer<T> where T : Mesh
    {
        public T Optimize(Chunk<Voxel> chunk);
    }
}