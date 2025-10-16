using System;

namespace Libs.VoxelMeshOptimizer
{
    public interface Voxel
    {
        public ushort ID { get; }
        public bool IsSolid { get; }
    }


    [Flags]
    public enum VoxelFace
    {
        NONE = 0,
        Zpos = 1 << 0, // Back
        Zneg = 1 << 1, // Front
        Xneg = 1 << 2, // Left
        Xpos = 1 << 3, // Right
        Ypos = 1 << 4, // Top
        Yneg = 1 << 5 // Bottom
    }
}