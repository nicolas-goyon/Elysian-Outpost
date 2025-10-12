namespace Libs.VoxelMeshOptimizer
{

    public class VoxelVisibilityMap
    {
        private VoxelFace[,,] visibilityMap;
        private Chunk<Voxel> chunk;

        public VoxelVisibilityMap(Chunk<Voxel> chunk)
        {
            this.chunk = chunk;
            visibilityMap = new VoxelFace[chunk.XDepth, chunk.YDepth, chunk.ZDepth];
            ComputeVisibilityMap();
        }

        private void ComputeVisibilityMap()
        {
            chunk.ForEachCoordinate(
                Axis.X, AxisOrder.Ascending,
                Axis.Y, AxisOrder.Ascending,
                Axis.Z, AxisOrder.Ascending,
                (uint x, uint y, uint z) =>
                {
                    Voxel voxel = chunk.Get(x, y, z);
                    if (voxel == null || !voxel.IsSolid)
                    {
                        visibilityMap[x, y, z] = VoxelFace.None;
                        return;
                    }

                    VoxelFace visibleFaces = VoxelFace.None;

                    // Check adjacent voxels
                    if (IsAdjacentVoxelTransparent(x, y, z + 1)) visibleFaces |= VoxelFace.Zpos;
                    if (IsAdjacentVoxelTransparent(x, y, z - 1)) visibleFaces |= VoxelFace.Zneg;
                    if (IsAdjacentVoxelTransparent(x - 1, y, z)) visibleFaces |= VoxelFace.Xneg;
                    if (IsAdjacentVoxelTransparent(x + 1, y, z)) visibleFaces |= VoxelFace.Xpos;
                    if (IsAdjacentVoxelTransparent(x, y + 1, z)) visibleFaces |= VoxelFace.Ypos;
                    if (IsAdjacentVoxelTransparent(x, y - 1, z)) visibleFaces |= VoxelFace.Yneg;

                    visibilityMap[x, y, z] = visibleFaces;
                });
        }

        private bool IsAdjacentVoxelTransparent(uint x, uint y, uint z)
        {
            if (chunk.IsOutOfBound(x, y, z)) return true;

            Voxel adjacentVoxel = chunk.Get(x, y, z);
            return adjacentVoxel == null || !adjacentVoxel.IsSolid;
        }

        public VoxelFace GetVisibleFaces(uint x, uint y, uint z)
        {
            if (x < 0 || x >= chunk.XDepth || y < 0 || y >= chunk.YDepth || z < 0 || z >= chunk.ZDepth)
                return VoxelFace.None;
            return visibilityMap[x, y, z];
        }
    }
}