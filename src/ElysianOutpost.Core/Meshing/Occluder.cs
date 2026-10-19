using Libs.VoxelMeshOptimizer.OcclusionAlgorithms.Common;

namespace Libs.VoxelMeshOptimizer.OcclusionAlgorithms
{


    public interface Occluder
    {
        /// <summary>
        /// Computes a collection of 2D planes (slices) in each direction,
        /// representing all visible faces in the chunk. Each plane references the voxels
        /// that have a face visible in that slice and direction.
        /// </summary>
        public VisibleFaces ComputeVisibleFaces();
    }
}