using System;
using System.Linq;
using Libs.VoxelMeshOptimizer.OcclusionAlgorithms;

namespace Libs.VoxelMeshOptimizer.OptimizationAlgorithms.DisjointSet
{

    public class DisjointSetMeshOptimizer : MeshOptimizer
    {
        private Mesh mesh;

        public DisjointSetMeshOptimizer(Mesh mesh)
        {
            if (!mesh.Quads.Any())
            {
                throw new ArgumentException("Mesh must be empty");
            }

            this.mesh = mesh;
        }


        public Mesh Optimize(Chunk<Voxel> chunk)
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