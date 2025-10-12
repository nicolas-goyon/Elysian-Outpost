namespace Libs.VoxelMeshOptimizer
{

    public interface MeshOptimizer
    {
        public Mesh Optimize(Chunk<Voxel> chunk);
    }
}