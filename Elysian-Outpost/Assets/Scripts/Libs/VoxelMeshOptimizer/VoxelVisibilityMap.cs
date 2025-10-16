namespace Libs.VoxelMeshOptimizer
{

    public class VoxelVisibilityMap
    {
        private readonly VoxelFace[,,] _visibilityMap;
        private readonly Chunk<Voxel> _chunk;

        public VoxelVisibilityMap(Chunk<Voxel> chunk)
        {
            _chunk = chunk;
            _visibilityMap = new VoxelFace[chunk.XDepth, chunk.YDepth, chunk.ZDepth];
            ComputeVisibilityMap();
        }

        private void ComputeVisibilityMap()
        {
            _chunk.ForEachCoordinate(
                Axis.X, AxisOrder.ASCENDING,
                Axis.Y, AxisOrder.ASCENDING,
                Axis.Z, AxisOrder.ASCENDING,
                (uint x, uint y, uint z) =>
                {
                    Voxel voxel = _chunk.Get(x, y, z);
                    if (voxel is not { IsSolid: true })
                    {
                        _visibilityMap[x, y, z] = VoxelFace.NONE;
                        return;
                    }

                    VoxelFace visibleFaces = VoxelFace.NONE;

                    // Check adjacent voxels
                    if (IsAdjacentVoxelTransparent(x, y, z + 1)) visibleFaces |= VoxelFace.Zpos;
                    if (IsAdjacentVoxelTransparent(x, y, z - 1)) visibleFaces |= VoxelFace.Zneg;
                    if (IsAdjacentVoxelTransparent(x - 1, y, z)) visibleFaces |= VoxelFace.Xneg;
                    if (IsAdjacentVoxelTransparent(x + 1, y, z)) visibleFaces |= VoxelFace.Xpos;
                    if (IsAdjacentVoxelTransparent(x, y + 1, z)) visibleFaces |= VoxelFace.Ypos;
                    if (IsAdjacentVoxelTransparent(x, y - 1, z)) visibleFaces |= VoxelFace.Yneg;

                    _visibilityMap[x, y, z] = visibleFaces;
                });
        }

        private bool IsAdjacentVoxelTransparent(uint x, uint y, uint z)
        {
            if (_chunk.IsOutOfBound(x, y, z)) return true;

            Voxel adjacentVoxel = _chunk.Get(x, y, z);
            return adjacentVoxel == null || !adjacentVoxel.IsSolid;
        }

        public VoxelFace GetVisibleFaces(uint x, uint y, uint z)
        {
            if (x >= _chunk.XDepth || y >= _chunk.YDepth || z >= _chunk.ZDepth)
                return VoxelFace.NONE;
            return _visibilityMap[x, y, z];
        }
    }
}