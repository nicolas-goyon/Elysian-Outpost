using System;

namespace Libs.VoxelMeshOptimizer
{
    public interface Chunk<out T> where T : Voxel
    {
        /// <summary>
        /// The chunk dimensions in voxels.
        /// </summary>
        public uint XDepth { get; }

        public uint YDepth { get; }
        public uint ZDepth { get; }

        /// <summary>
        /// Iterates over every position in this chunk in the order of three distinct axes.
        /// For example, (TopToBottom, LeftToRight, FrontToBack) or any other combination,
        /// as long as no two parameters share the same underlying axis.
        /// 
        /// Terminology:
        /// - MajorAxis: First axis iterated in the nested loops, also known as the "slice" axis (outermost loop).
        /// - MiddleAxis: Second axis iterated (middle loop).
        /// - MinorAxis: Third axis iterated (innermost loop).
        ///
        /// The callback receives a <see cref="VoxelPosition"/> that encapsulates:
        /// - The absolute (X, Y, Z) coordinate.
        /// - The <see cref="HumanAxis"/> values (MajorAxis, MiddleAxis, MinorAxis) for debugging or advanced logic.
        /// </summary>
        public void ForEachCoordinate(
            Axis major, AxisOrder majorAsc,
            Axis middle, AxisOrder middleAsc,
            Axis minor, AxisOrder minorAsc,
            Action<uint, uint, uint> action
        );

        /// <summary>
        /// Retrieves the voxel at the given position. The position contains both
        /// the absolute coordinates (X,Y,Z) and the iteration axes used to obtain them.
        /// If out of range, this can throw or return null based on your design choice.
        /// </summary>
        public T Get(uint x, uint y, uint z);


        /// <summary>
        /// Returns the 2D plane dimensions (width, height) for the chunk
        /// based on the chosen axes.
        /// 
        /// For example, if (major, middle, minor) = 
        /// (FrontToBack, BottomToTop, LeftToRight),
        /// - The "line" dimension (minor) is X (left-to-right).
        /// - The "plane" dimension is then (X by Y).
        /// - The "cube" dimension adds the major axis Z.
        /// Thus, <c>GetPlaneDimensions</c> would return (chunk.Width, chunk.Height).
        /// </summary>
        public (uint planeWidth, uint planeHeight) GetPlaneDimensions(Axis major, Axis middle, Axis minor);

        /// <summary>
        /// Simple helper to pick the chunk’s dimension (depth) by axis.
        /// </summary>
        public uint GetDepth(Axis axis);

        public bool IsOutOfBound(uint x, uint y, uint z);

        public bool AreDifferentAxis(Axis major, Axis middle, Axis minor);

    }
}