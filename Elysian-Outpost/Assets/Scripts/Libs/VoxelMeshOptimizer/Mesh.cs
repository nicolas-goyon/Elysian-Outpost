using System.Collections.Generic;

namespace Libs.VoxelMeshOptimizer
{

    public interface Mesh
    {
        List<MeshQuad> Quads { get; }
    }
}