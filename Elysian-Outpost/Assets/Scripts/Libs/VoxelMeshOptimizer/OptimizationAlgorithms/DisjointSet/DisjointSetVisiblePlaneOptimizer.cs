using System.Numerics;
using Libs.VoxelMeshOptimizer.OcclusionAlgorithms.Common;
using Libs.Security;  // Add Guard namespace
using System.Collections.Generic;  // For List and Dictionary
using System;
using System.Linq; // For Exception

namespace Libs.VoxelMeshOptimizer.OptimizationAlgorithms.DisjointSet
{
    /// <summary>
    /// Optimizes a 2D VisiblePlane by merging contiguous regions of solid voxels with the same ID.
    /// </summary>
    public class DisjointSetVisiblePlaneOptimizer
    {
        private readonly DisjointSet _disjointSet;
        private readonly VisiblePlane _plane;
        private readonly Voxel?[,] _voxels;
        private readonly int _width;
        private readonly int _height;
        private readonly Chunk<Voxel> _chunk;

        public DisjointSetVisiblePlaneOptimizer(VisiblePlane plane, Chunk<Voxel> chunk)
        {
            Guard.IsNotNull(plane, nameof(plane));
            Guard.IsNotNull(plane.Voxels, nameof(plane.Voxels));
            _plane = plane;
            _voxels = plane.Voxels;
            _chunk = chunk;

            _width = _voxels.GetLength(0);
            _height = _voxels.GetLength(1);

            Guard.IsGreaterThan(_width, 0, nameof(_width));
            Guard.IsGreaterThan(_height, 0, nameof(_height));

            _disjointSet = new DisjointSet(_width * _height);
        }

        public void Optimize()
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (_voxels[x, y] == null || IsNotAlone(x, y))
                        continue;

                    CreateOneSet(x, y);
                }
            }
        }

        private void CreateOneSet(int x, int y)
        {
            Guard.IsInRange(x, 0, _width, nameof(x));
            Guard.IsInRange(y, 0, _height, nameof(y));

            Voxel rootVoxel = _voxels[x, y];
            if (rootVoxel == null) return;

            int currentWidth = 1;
            int currentHeight = 1;

            // Expand to the right
            while (x + currentWidth < _width &&
                   _voxels[x + currentWidth, y]?.ID == rootVoxel.ID &&
                   !IsNotAlone(x + currentWidth, y))
            {
                currentWidth++;
            }

            // Expand downward
            while (y + currentHeight < _height)
            {
                bool canExpand = true;
                for (int dx = 0; dx < currentWidth; dx++)
                {
                    Voxel v = _voxels[x + dx, y + currentHeight];
                    if (v?.ID != rootVoxel.ID || IsNotAlone(x + dx, y + currentHeight))
                    {
                        canExpand = false;
                        break;
                    }
                }

                if (!canExpand) break;
                currentHeight++;
            }

            // Union the whole block
            int rootIndex = ToIndex(x, y);
            for (int dy = 0; dy < currentHeight; dy++)
            {
                for (int dx = 0; dx < currentWidth; dx++)
                {
                    _disjointSet.Union(rootIndex, ToIndex(x + dx, y + dy));
                }
            }
        }

        private bool IsNotAlone(int x, int y)
        {
            Guard.IsInRange(x, 0, _width, nameof(x));
            Guard.IsInRange(y, 0, _height, nameof(y));

            Voxel voxel = _voxels[x, y];
            if (voxel == null) return true;

            int root = _disjointSet.Find(ToIndex(x, y));
            if (root != ToIndex(x, y)) return true;

            return (x > 0 && AreSame(x, y, x - 1, y)) ||
                   (x < _width - 1 && AreSame(x, y, x + 1, y)) ||
                   (y > 0 && AreSame(x, y, x, y - 1)) ||
                   (y < _height - 1 && AreSame(x, y, x, y + 1));
        }

        private bool AreSame(int x1, int y1, int x2, int y2)
        {
            Voxel v1 = _voxels[x1, y1];
            Voxel v2 = _voxels[x2, y2];
            if (v1 == null || v2 == null) return false;
            return v1.ID == v2.ID &&
                   _disjointSet.Find(ToIndex(x2, y2)) == _disjointSet.Find(ToIndex(x1, y1));
        }

        private int ToIndex(int x, int y) => y * _width + x;


        public List<MeshQuad> ToMeshQuads()
        {
            Dictionary<int, List<(int x, int y)>> groups = new Dictionary<int, List<(int x, int y)>>();


            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (_voxels[x, y] == null) continue;

                    int root = _disjointSet.Find(ToIndex(x, y));
                    if (!groups.ContainsKey(root))
                    {
                        groups[root] = new List<(int x, int y)>();
                    }

                    groups[root].Add((x, y));
                }
            }




            List<MeshQuad> quads = new List<MeshQuad>();
            MeshQuad? quad;
            foreach (KeyValuePair<int, List<(int x, int y)>> group in groups)
            {

                List<(int x, int y)> groupVoxels = group.Value;

                int minX = groupVoxels.Min(p => p.x);
                int maxX = groupVoxels.Max(p => p.x);
                int minY = groupVoxels.Min(p => p.y);
                int maxY = groupVoxels.Max(p => p.y);


                // int width = maxX - minX + 1; // Not used
                // int height = maxY - minY + 1;



                uint voxelId = _voxels[minX, minY]!.ID;


                switch (_plane.MajorAxis, _plane.MajorAxisOrder)
                {
                    case (Axis.X, AxisOrder.DESCENDING):
                    {
                        uint x = _chunk.XDepth - _plane.SliceIndex;
                        long y1 = _chunk.YDepth - minX;
                        long y2 = _chunk.YDepth - maxX - 1;
                        long z1 = _chunk.ZDepth - minY;
                        long z2 = _chunk.ZDepth - maxY - 1;
                        quad = new MeshQuad
                        {
                            Vertex0 = new Vector3(x, y1, z1),
                            Vertex1 = new Vector3(x, y2, z1),
                            Vertex2 = new Vector3(x, y2, z2),
                            Vertex3 = new Vector3(x, y1, z2),
                            Normal = new Vector3(1, 0, 0),
                            VoxelID = voxelId
                        };
                        break;
                    }
                    case (Axis.X, AxisOrder.ASCENDING):
                    {

                        uint x = _plane.SliceIndex;
                        int y1 = minX;
                        int y2 = maxX + 1;
                        int z1 = minY;
                        int z2 = maxY + 1;
                        quad = new MeshQuad
                        {
                            Vertex1 = new Vector3(x, y1, z1),
                            Vertex0 = new Vector3(x, y2, z1),
                            Vertex3 = new Vector3(x, y2, z2),
                            Vertex2 = new Vector3(x, y1, z2),
                            Normal = new Vector3(1, 0, 0),
                            VoxelID = voxelId
                        };
                        break;
                    }
                    case (Axis.Y, AxisOrder.DESCENDING):
                    {
                        long x1 = _chunk.XDepth - minY;
                        long x2 = _chunk.XDepth - maxY - 1;
                        uint y = _chunk.YDepth - _plane.SliceIndex;
                        long z1 = _chunk.ZDepth - minX;
                        long z2 = _chunk.ZDepth - maxX - 1;
                        quad = new MeshQuad
                        {
                            Vertex0 = new Vector3(x1, y, z1),
                            Vertex1 = new Vector3(x1, y, z2),
                            Vertex2 = new Vector3(x2, y, z2),
                            Vertex3 = new Vector3(x2, y, z1),
                            Normal = new Vector3(1, 0, 0),
                            VoxelID = voxelId
                        };
                        break;
                    }
                    case (Axis.Y, AxisOrder.ASCENDING):
                    {
                        int x1 = minY;
                        int x2 = maxY + 1;
                        uint y = _plane.SliceIndex;
                        int z1 = minX;
                        int z2 = maxX + 1;
                        quad = new MeshQuad
                        {
                            Vertex1 = new Vector3(x1, y, z1),
                            Vertex0 = new Vector3(x1, y, z2),
                            Vertex3 = new Vector3(x2, y, z2),
                            Vertex2 = new Vector3(x2, y, z1),
                            Normal = new Vector3(1, 0, 0),
                            VoxelID = voxelId
                        };
                        break;
                    }
                    case (Axis.Z, AxisOrder.DESCENDING):
                    {
                        long x1 = _chunk.XDepth - minY;
                        long x2 = _chunk.XDepth - maxY - 1;
                        long y1 = _chunk.YDepth - minX;
                        long y2 = _chunk.YDepth - maxX - 1;
                        uint z = _chunk.ZDepth - _plane.SliceIndex;
                        quad = new MeshQuad
                        {
                            Vertex1 = new Vector3(x1, y1, z),
                            Vertex0 = new Vector3(x1, y2, z),
                            Vertex3 = new Vector3(x2, y2, z),
                            Vertex2 = new Vector3(x2, y1, z),
                            Normal = new Vector3(1, 0, 0),
                            VoxelID = voxelId
                        };
                        break;
                    }
                    case (Axis.Z, AxisOrder.ASCENDING):
                    {
                        int x1 = minY;
                        int x2 = maxY + 1;
                        int y1 = minX;
                        int y2 = maxX + 1;
                        uint z = _plane.SliceIndex;
                        quad = new MeshQuad
                        {
                            Vertex0 = new Vector3(x1, y1, z),
                            Vertex1 = new Vector3(x1, y2, z),
                            Vertex2 = new Vector3(x2, y2, z),
                            Vertex3 = new Vector3(x2, y1, z),
                            Normal = new Vector3(1, 0, 0),
                            VoxelID = voxelId
                        };
                        break;
                    }
                    default: throw new ArgumentOutOfRangeException();
                }


                if (quad == null) throw new Exception("Unexpected null value");

                quads.Add(quad);
            }

            return quads;
        }

    }
}