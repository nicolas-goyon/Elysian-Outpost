

using System.Collections.Generic;
using System.Data;
using System.Linq;
using Libs.VoxelMeshOptimizer.OcclusionAlgorithms.Common;
using UnityEngine;

namespace Libs.VoxelMeshOptimizer.OcclusionAlgorithms
{
    /// <summary>
    /// Optimizes voxel occlusion by computing visible voxel planes based on the provided voxel chunk.
    /// </summary>
    public class VoxelOcclusionOptimizer : Occluder
    {
        /// <summary>
        /// The voxel chunk to be processed.
        /// </summary>
        private readonly Chunk<Voxel> chunk;

        /// <summary>
        /// The visibility map generated from the voxel chunk.
        /// </summary>
        private readonly VoxelVisibilityMap visibilityMap;

        /// <summary>
        /// Initializes a new instance of the <see cref="VoxelOcclusionOptimizer"/> class.
        /// </summary>
        /// <param name="chunk">The voxel chunk to optimize.</param>
        public VoxelOcclusionOptimizer(Chunk<Voxel> chunk)
        {
            if (chunk == null) throw new NoNullAllowedException();

            this.chunk = chunk;
            visibilityMap = new VoxelVisibilityMap(chunk);
        }

        /// <summary>
        /// Computes and returns the visible voxel planes for all axes and their orientations.
        /// </summary>
        /// <returns>
        /// A <see cref="VisibleFaces"/> instance containing the visible planes grouped by axis and order.
        /// </returns>
        public VisibleFaces ComputeVisibleFaces()
        {
            var result = new VisibleFaces();

            result.PlanesByAxis[(Axis.X, AxisOrder.ASCENDING)] = BuildPlanesForAxis(Axis.X, AxisOrder.ASCENDING);
            result.PlanesByAxis[(Axis.X, AxisOrder.DESCENDING)] = BuildPlanesForAxis(Axis.X, AxisOrder.DESCENDING);

            result.PlanesByAxis[(Axis.Y, AxisOrder.ASCENDING)] = BuildPlanesForAxis(Axis.Y, AxisOrder.ASCENDING);
            result.PlanesByAxis[(Axis.Y, AxisOrder.DESCENDING)] = BuildPlanesForAxis(Axis.Y, AxisOrder.DESCENDING);

            result.PlanesByAxis[(Axis.Z, AxisOrder.ASCENDING)] = BuildPlanesForAxis(Axis.Z, AxisOrder.ASCENDING);
            result.PlanesByAxis[(Axis.Z, AxisOrder.DESCENDING)] = BuildPlanesForAxis(Axis.Z, AxisOrder.DESCENDING);

            return result;
        }

        /// <summary>
        /// Builds the visible planes for a specified slicing axis and order.
        /// </summary>
        /// <param name="sliceAxis">The axis that defines the slicing orientation.</param>
        /// <param name="axisOrder">The order (ascending or descending) in which to iterate over the slice.</param>
        /// <returns>
        /// A list of <see cref="VisiblePlane"/> objects representing the visible voxel planes for the provided slice.
        /// </returns>
        /// <remarks>
        /// The method maps the slice axis to the appropriate voxel face using a bitmask and 
        /// determines the iteration order via a helper function (<see cref="AxisExtensions.DefineIterationOrder"/>).
        /// This helper also computes the corresponding 2D plane dimensions and translates 
        /// 3D voxel coordinates to 2D plane positions using <see cref="AxisExtensions.GetSlicePlanePosition"/>.
        /// This design choice encapsulates complex logic within a dedicated helper, aiding maintainability.
        /// </remarks>
        private List<VisiblePlane> BuildPlanesForAxis(Axis sliceAxis, AxisOrder axisOrder)
        {
            // Map the slice axis to the corresponding voxel face flag.
            var faceFlag = AxisExtensions.ToVoxelFace(sliceAxis, axisOrder);

            // Determine the order in which the voxels are iterated.
            var (majorA, majorAO, middleA, middleAO, minorA, minorAO) =
                AxisExtensions.DefineIterationOrder(sliceAxis, axisOrder);

            // Retrieve the dimensions of the 2D plane slice.
            (uint planeWidth, uint planeHeight) = chunk.GetPlaneDimensions(majorA, middleA, minorA);

            // Dictionary to store the visible planes keyed by the slice index.
            uint sliceCount = chunk.GetDepth(sliceAxis);
            VisiblePlane[] planesBySlice = new VisiblePlane[sliceCount];

            chunk.ForEachCoordinate(
                major: majorA, majorAsc: majorAO,
                middle: middleA, middleAsc: middleAO,
                minor: minorA, minorAsc: minorAO,
                (uint x, uint y, uint z) =>
                {
                    VoxelFace faces = visibilityMap.GetVisibleFaces(x, y, z);
                    if (!faces.HasFlag(faceFlag)) return;

                    // Retrieve the current slice index.
                    uint sliceIndex = AxisExtensions.GetDepthFromAxis(sliceAxis, axisOrder, x, y, z, chunk);

                    // Select the appropriate visible plane.
                    planesBySlice[sliceIndex] ??= new VisiblePlane(
                        majorA, majorAO,
                        middleA, middleAO,
                        minorA, minorAO,
                        sliceIndex,
                        planeWidth, planeHeight
                    );

                    VisiblePlane plane = planesBySlice[sliceIndex];

                    // Compute the 2D position on the plane from the 3D coordinates.
                    (uint planeX, uint planeY) = AxisExtensions.GetSlicePlanePosition(
                        majorA, majorAO,
                        middleA, middleAO,
                        minorA, minorAO,
                        x, y, z, chunk);

                    plane.Voxels[planeX, planeY] = chunk.Get(x, y, z);
                }
            );

            // Gather the resulting non-empty planes.
            List<VisiblePlane> result = planesBySlice.Where(plane => plane is { IsPlaneEmpty: false }).ToList();

            return result;
        }
    }
}