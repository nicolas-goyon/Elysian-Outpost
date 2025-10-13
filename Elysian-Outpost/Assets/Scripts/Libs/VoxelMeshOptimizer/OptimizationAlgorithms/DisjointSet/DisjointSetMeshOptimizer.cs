using System;
using System.Linq;
using Libs.VoxelMeshOptimizer.OcclusionAlgorithms;

namespace Libs.VoxelMeshOptimizer.OptimizationAlgorithms.DisjointSet
{

    public class DisjointSetMeshOptimizer<T> : MeshOptimizer<T> where T : Mesh
    {
        private T mesh;

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

            this.mesh = mesh;
        }


        public T Optimize(Chunk<Voxel> chunk)
        {
            var occluder = new VoxelOcclusionOptimizer(chunk);
            var visibileFaces = occluder.ComputeVisibleFaces();

            foreach (var visibleFace in visibileFaces.PlanesByAxis)
            {
                foreach (var face in visibleFace.Value)
                {
                    var optimizer = new DisjointSetVisiblePlaneOptimizer(face, chunk);
                    optimizer.Optimize();
                    var quads = optimizer.ToMeshQuads();
                    mesh.Quads.AddRange(quads);
                }
            }

            return mesh;
        }
    }
}