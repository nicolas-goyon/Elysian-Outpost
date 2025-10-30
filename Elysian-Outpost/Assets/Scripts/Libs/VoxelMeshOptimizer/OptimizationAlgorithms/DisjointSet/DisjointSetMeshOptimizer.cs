using System;
using System.Collections.Generic;
using System.Linq;
using Libs.VoxelMeshOptimizer.OcclusionAlgorithms;
using Libs.VoxelMeshOptimizer.OcclusionAlgorithms.Common;

namespace Libs.VoxelMeshOptimizer.OptimizationAlgorithms.DisjointSet
{

    public class DisjointSetMeshOptimizer<T> : MeshOptimizer<T> where T : Mesh
    {
        private T _mesh;

        public DisjointSetMeshOptimizer(T mesh)
        {
            if (mesh == null)
            {
                throw new ArgumentNullException(nameof(mesh));
            }
            
            if (mesh.Quads == null)
            {
                throw new ArgumentException("Mesh quads cannot be null");
            }
            
            if (mesh.Quads.Any())
            {
                throw new ArgumentException($"Mesh must be empty, currently has { mesh.Quads.Count } quads");
            }

            this._mesh = mesh;
        }


        public T Optimize(Chunk<Voxel> chunk)
        {
            VoxelOcclusionOptimizer occluder = new(chunk);
            VisibleFaces visibleFaces = occluder.ComputeVisibleFaces();

            // TODO : Check diff
            // foreach (DisjointSetVisiblePlaneOptimizer optimizer in from visibleFace in visibleFaces.PlanesByAxis from face in visibleFace.Value select new DisjointSetVisiblePlaneOptimizer(face, chunk))
            // {
            //     optimizer.Optimize();
            //     List<MeshQuad> quads = optimizer.ToMeshQuads();
            //     _mesh.Quads.AddRange(quads);
            // }
            
            foreach (var visibleFace in visibleFaces.PlanesByAxis)
            {
                foreach (var face in visibleFace.Value)
                {
                    var optimizer = new DisjointSetVisiblePlaneOptimizer(face, chunk);
                    optimizer.Optimize();
                    var quads = optimizer.ToMeshQuads();
                    _mesh.Quads.AddRange(quads);
                }
            }

            return _mesh;
        }
    }
}