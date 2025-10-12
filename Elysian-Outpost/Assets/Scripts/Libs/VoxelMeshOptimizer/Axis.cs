using System;
using System.Numerics;

namespace Libs.VoxelMeshOptimizer
{

    /// <summary>
    /// Terminology:
    /// - MajorAxis: First axis iterated in the nested loops, also known as the "slice" axis (outermost loop).
    /// - MiddleAxis: Second axis iterated (middle loop).
    /// - MinorAxis: Third axis iterated (innermost loop).
    /// </summary>
    public enum Axis
    {
        X,
        Y,
        Z
    }

    public enum AxisOrder
    {
        Ascending,
        Descending
    }


    public static class AxisExtensions
    {
        public static VoxelFace ToVoxelFace(Axis axis, AxisOrder axisOrder)
        {
            return (axis, axisOrder) switch
            {
                (Axis.X, AxisOrder.Ascending) => VoxelFace.Xneg,
                (Axis.X, AxisOrder.Descending) => VoxelFace.Xpos,

                (Axis.Y, AxisOrder.Ascending) => VoxelFace.Yneg,
                (Axis.Y, AxisOrder.Descending) => VoxelFace.Ypos,

                (Axis.Z, AxisOrder.Ascending) => VoxelFace.Zneg,
                (Axis.Z, AxisOrder.Descending) => VoxelFace.Zpos,

                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        /// <summary>
        /// Returns the "depth" of the given coordinate when viewed from a face determined
        /// by the specified axis and axis order.
        /// </summary>
        /// <param name="axis">The axis along which to measure the depth (X, Y or Z).</param>
        /// <param name="axisOrder">The order (Ascending for front/low index, Descending for back/high index).</param>
        /// <param name="x">The X coordinate of the voxel.</param>
        /// <param name="y">The Y coordinate of the voxel.</param>
        /// <param name="z">The Z coordinate of the voxel.</param>
        /// <param name="chunk">The chunk containing the voxel.</param>
        /// <returns>The depth of the voxel relative to the face specified by axis and order.</returns>
        public static uint GetDepthFromAxis(
            Axis axis,
            AxisOrder axisOrder,
            uint x,
            uint y,
            uint z,
            Chunk<Voxel> chunk)
        {
            if (chunk.IsOutOfBound(x, y, z)) throw new ArgumentOutOfRangeException();

            // Select the relevant coordinate based on the axis.
            uint relativeDepth = axis switch
            {
                Axis.X => x,
                Axis.Y => y,
                Axis.Z => z,
                _ => throw new ArgumentException("Invalid axis", nameof(axis))
            };

            // Get the total depth of the chunk along the selected axis.
            uint totalDepth = chunk.GetDepth(axis);

            // Calculate the depth from the face determined by the axis order.
            if (axisOrder == AxisOrder.Ascending) return relativeDepth;
            else return totalDepth - 1 - relativeDepth;
        }

        public static void SetAxis(Vector3 vector, Axis axis, float value)
        {
            switch (axis)
            {
                case Axis.X: vector.X = value; break;
                case Axis.Y: vector.Y = value; break;
                case Axis.Z: vector.Z = value; break;
            }
        }

        public static Vector3 Direction(Axis axis, AxisOrder order)
        {
            float sign = order == AxisOrder.Ascending ? -1f : 1f;
            return axis switch
            {
                Axis.X => new Vector3(sign, 0, 0),
                Axis.Y => new Vector3(0, sign, 0),
                Axis.Z => new Vector3(0, 0, sign),
                _ => throw new ArgumentOutOfRangeException()
            };
        }


        /// <summary>
        /// Defines the iteration axes and their orders for looping through a chunk.
        /// The iteration follows these roles:
        /// - MajorAxis: The outermost loop (a.k.a. the "slice" axis).
        /// - MiddleAxis: The second loop.
        /// - MinorAxis: The innermost loop.
        ///
        /// This method picks the two axes not used as the major axis using a default mapping.
        /// The default mappings are:
        ///   - If major is X, then middle = Y and minor = Z.
        ///   - If major is Y, then middle = Z and minor = X.
        ///   - If major is Z, then middle = Y and minor = X.
        ///
        /// All axes will use the same order as the provided major axis order.
        /// </summary>
        /// <param name="majorAxis">The axis to be used as the outermost (slice) axis.</param>
        /// <param name="majorOrder">The order to use for the major axis (and by default for the others).</param>
        /// <returns>
        /// A tuple containing the axes and their orders in the following order: 
        /// (majorAxis, majorOrder, middleAxis, middleOrder, minorAxis, minorOrder).
        /// </returns>
        public static (Axis major, AxisOrder majorOrder, Axis middle, AxisOrder middleOrder, Axis minor, AxisOrder
            minorOrder) DefineIterationOrder(Axis majorAxis, AxisOrder majorOrder)
        {
            Axis middle, minor;

            switch (majorAxis)
            {
                case Axis.X:
                    // With X as major, choose Y for middle and Z for minor.
                    middle = Axis.Y;
                    minor = Axis.Z;
                    break;
                case Axis.Y:
                    // With Y as major, choose Z for middle and X for minor.
                    middle = Axis.Z;
                    minor = Axis.X;
                    break;
                case Axis.Z:
                    // For Z (interpreted as FrontToBack), choose Y for middle (BottomToTop)
                    // and X for minor (LeftToRight) so that the iteration reflects:
                    // (FrontToBack, BottomToTop, LeftToRight)
                    middle = Axis.Y;
                    minor = Axis.X;
                    break;
                default:
                    throw new ArgumentException("Invalid major axis", nameof(majorAxis));
            }

            // Here we assign the same order to the remaining axes.
            AxisOrder middleOrder = majorOrder;
            AxisOrder minorOrder = majorOrder;

            return (majorAxis, majorOrder, middle, middleOrder, minor, minorOrder);
        }


        /// <summary>
        /// Maps the given voxel coordinates in the chunk into a 2D coordinate on a slice plane.
        /// The first axis is considered the "slice" axis (perpendicular to the plane), and
        /// the other two define the reading (in-plane) order.
        ///
        /// Each in-plane coordinate is adjusted based on the corresponding axis order:
        /// if the order is Ascending, the coordinate is used as-is; if Descending,
        /// the coordinate is “flipped” relative to the total depth along that axis.
        /// </summary>
        /// <param name="sliceAxis">The axis perpendicular to the slice plane (for checking distinctness).</param>
        /// <param name="sliceOrder">The face (order) for the slice axis (not used in calculation here, but may be useful for debugging or consistency).</param>
        /// <param name="planeAxis1">The axis that will determine the X coordinate on the slice plane.</param>
        /// <param name="planeAxis1Order">The order (Ascending/Descending) for the axis that will map to X.</param>
        /// <param name="planeAxis2">The axis that will determine the Y coordinate on the slice plane.</param>
        /// <param name="planeAxis2Order">The order (Ascending/Descending) for the axis that will map to Y.</param>
        /// <param name="x">The absolute x-coordinate of the voxel.</param>
        /// <param name="y">The absolute y-coordinate of the voxel.</param>
        /// <param name="z">The absolute z-coordinate of the voxel.</param>
        /// <param name="chunk">The chunk in which the voxel resides.</param>
        /// <returns>A tuple (x, y) representing the voxel’s position on the slice plane.</returns>
        public static (uint planeX, uint planeY) GetSlicePlanePosition(
            Axis sliceAxis, AxisOrder sliceOrder,
            Axis planeAxis1, AxisOrder planeAxis1Order,
            Axis planeAxis2, AxisOrder planeAxis2Order,
            uint x, uint y, uint z,
            Chunk<Voxel> chunk)
        {
            // Ensure that all three axes are distinct.
            if (!chunk.AreDifferentAxis(sliceAxis, planeAxis1, planeAxis2))
                throw new ArgumentException("All axes must be distinct.");

            // Determine the coordinate on the plane for planeAxis1 (mapped to the X coordinate on the slice plane).
            uint coordForPlane1 = planeAxis1 switch
            {
                Axis.X => x,
                Axis.Y => y,
                Axis.Z => z,
                _ => throw new ArgumentException("Invalid axis for planeAxis1")
            };

            // Get the corresponding total dimension for planeAxis1.
            uint totalPlane1 = chunk.GetDepth(planeAxis1);
            uint planeX = planeAxis1Order == AxisOrder.Ascending
                ? coordForPlane1
                : totalPlane1 - 1 - coordForPlane1;

            // Determine the coordinate on the plane for planeAxis2 (mapped to the Y coordinate on the slice plane).
            uint coordForPlane2 = planeAxis2 switch
            {
                Axis.X => x,
                Axis.Y => y,
                Axis.Z => z,
                _ => throw new ArgumentException("Invalid axis for planeAxis2")
            };

            // Get the corresponding total dimension for planeAxis2.
            uint totalPlane2 = chunk.GetDepth(planeAxis2);
            uint planeY = planeAxis2Order == AxisOrder.Ascending
                ? coordForPlane2
                : totalPlane2 - 1 - coordForPlane2;

            return (planeX, planeY);
        }

    }
}