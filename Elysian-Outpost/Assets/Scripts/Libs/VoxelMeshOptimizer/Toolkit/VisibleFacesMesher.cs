
using System;
using System.Collections.Generic;
using System.Numerics;
using Libs.VoxelMeshOptimizer.OcclusionAlgorithms.Common;

namespace Libs.VoxelMeshOptimizer.Toolkit
{
    /// <summary>
    /// Utility class that converts <see cref="VisibleFaces"/> produced by
    /// <see cref="VoxelOcclusionOptimizer"/> into a list of <see cref="MeshQuad"/>.
    /// This does not perform any further optimization; each visible voxel face
    /// results in one quad in the output mesh.
    /// </summary>
    public static class VisibleFacesMesher
    {
        /// <summary>
        /// Builds a list of quads for every visible voxel face contained in
        /// <paramref name="visibleFaces"/>.
        /// </summary>
        /// <param name="visibleFaces">The result of <see cref="VoxelOcclusionOptimizer.ComputeVisibleFaces"/>.</param>
        /// <param name="chunk">The chunk from which the visibility information was computed.</param>
        /// <returns>A list of quads representing each visible voxel face.</returns>
        public static List<MeshQuad> Build(VisibleFaces visibleFaces, Chunk<Voxel> chunk)
        {
            List<MeshQuad> quads = new List<MeshQuad>();

            foreach (KeyValuePair<(Axis, AxisOrder), List<VisiblePlane>> kvp in visibleFaces.PlanesByAxis)
            {
                Axis sliceAxis = kvp.Key.Item1;
                AxisOrder axisOrder = kvp.Key.Item2;
                foreach (VisiblePlane plane in kvp.Value)
                {
                    uint width = (uint)plane.Voxels.GetLength(0);
                    uint height = (uint)plane.Voxels.GetLength(1);

                    for (uint px = 0; px < width; px++)
                    {
                        for (uint py = 0; py < height; py++)
                        {
                            Voxel voxel = plane.Voxels[px, py];
                            if (voxel == null || !voxel.IsSolid) continue;

                            // reconstruct absolute coordinates
                            (uint x, uint y, uint z) coords = ReconstructCoordinates(plane, px, py, chunk);
                            quads.Add(CreateQuad(coords.x, coords.y, coords.z, voxel.ID, sliceAxis, axisOrder));
                        }
                    }
                }
            }

            return quads;
        }

        private static (uint x, uint y, uint z) ReconstructCoordinates(VisiblePlane plane, uint planeX, uint planeY,
            Chunk<Voxel> chunk)
        {
            uint majorCoord = plane.MajorAxisOrder == AxisOrder.Ascending
                ? plane.SliceIndex
                : chunk.GetDepth(plane.MajorAxis) - 1 - plane.SliceIndex;

            uint middleCoord = plane.MiddleAxisOrder == AxisOrder.Ascending
                ? planeX
                : chunk.GetDepth(plane.MiddleAxis) - 1 - planeX;

            uint minorCoord = plane.MinorAxisOrder == AxisOrder.Ascending
                ? planeY
                : chunk.GetDepth(plane.MinorAxis) - 1 - planeY;

            uint x = 0, y = 0, z = 0;
            switch (plane.MajorAxis)
            {
                case Axis.X: x = majorCoord; break;
                case Axis.Y: y = majorCoord; break;
                case Axis.Z: z = majorCoord; break;
            }

            switch (plane.MiddleAxis)
            {
                case Axis.X: x = middleCoord; break;
                case Axis.Y: y = middleCoord; break;
                case Axis.Z: z = middleCoord; break;
            }

            switch (plane.MinorAxis)
            {
                case Axis.X: x = minorCoord; break;
                case Axis.Y: y = minorCoord; break;
                case Axis.Z: z = minorCoord; break;
            }

            return (x, y, z);
        }

        private static MeshQuad CreateQuad(uint x, uint y, uint z, ushort voxelId, Axis axis, AxisOrder order)
        {
            float bx = x;
            float by = y;
            float bz = z;

            return (axis, order) switch
            {
                (Axis.X, AxisOrder.Descending) => new MeshQuad
                {
                    Vertex0 = new Vector3(bx + 1, by, bz + 1),
                    Vertex1 = new Vector3(bx + 1, by, bz),
                    Vertex2 = new Vector3(bx + 1, by + 1, bz),
                    Vertex3 = new Vector3(bx + 1, by + 1, bz + 1),
                    Normal = new Vector3(1, 0, 0),
                    VoxelID = voxelId
                },
                (Axis.X, AxisOrder.Ascending) => new MeshQuad
                {
                    Vertex0 = new Vector3(bx, by, bz),
                    Vertex1 = new Vector3(bx, by, bz + 1),
                    Vertex2 = new Vector3(bx, by + 1, bz + 1),
                    Vertex3 = new Vector3(bx, by + 1, bz),
                    Normal = new Vector3(-1, 0, 0),
                    VoxelID = voxelId
                },
                (Axis.Y, AxisOrder.Descending) => new MeshQuad
                {
                    Vertex0 = new Vector3(bx, by + 1, bz + 1),
                    Vertex1 = new Vector3(bx + 1, by + 1, bz + 1),
                    Vertex2 = new Vector3(bx + 1, by + 1, bz),
                    Vertex3 = new Vector3(bx, by + 1, bz),
                    Normal = new Vector3(0, 1, 0),
                    VoxelID = voxelId
                },
                (Axis.Y, AxisOrder.Ascending) => new MeshQuad
                {
                    Vertex0 = new Vector3(bx, by, bz),
                    Vertex1 = new Vector3(bx + 1, by, bz),
                    Vertex2 = new Vector3(bx + 1, by, bz + 1),
                    Vertex3 = new Vector3(bx, by, bz + 1),
                    Normal = new Vector3(0, -1, 0),
                    VoxelID = voxelId
                },
                (Axis.Z, AxisOrder.Descending) => new MeshQuad
                {
                    Vertex0 = new Vector3(bx, by, bz + 1),
                    Vertex1 = new Vector3(bx + 1, by, bz + 1),
                    Vertex2 = new Vector3(bx + 1, by + 1, bz + 1),
                    Vertex3 = new Vector3(bx, by + 1, bz + 1),
                    Normal = new Vector3(0, 0, 1),
                    VoxelID = voxelId
                },
                (Axis.Z, AxisOrder.Ascending) => new MeshQuad
                {
                    Vertex0 = new Vector3(bx, by, bz),
                    Vertex1 = new Vector3(bx, by + 1, bz),
                    Vertex2 = new Vector3(bx + 1, by + 1, bz),
                    Vertex3 = new Vector3(bx + 1, by, bz),
                    Normal = new Vector3(0, 0, -1),
                    VoxelID = voxelId
                },
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}