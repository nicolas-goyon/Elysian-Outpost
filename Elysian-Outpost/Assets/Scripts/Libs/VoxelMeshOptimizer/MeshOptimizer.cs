using Base;

namespace Libs.VoxelMeshOptimizer
{

    public interface MeshOptimizer
    {
        public Mesh Optimize(Chunk chunk);
    }
}