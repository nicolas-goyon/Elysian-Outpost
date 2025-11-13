using System;
using System.Collections.Generic;
using Libs.VoxelMeshOptimizer;
using Libs.VoxelMeshOptimizer.OptimizationAlgorithms.DisjointSet;

namespace Base
{
    public readonly struct ChunkMeshGenerationWorker
    {
        private readonly Chunk _chunk;

        public ChunkMeshGenerationWorker(Chunk chunk)
        {
            _chunk = chunk;
        }

        public (Chunk chunk, Mesh) Execute()
        {
            DisjointSetMeshOptimizer optimizer = new(new Mesh(new List<MeshQuad>()));
            return (_chunk, optimizer.Optimize(_chunk));
        }
    }
}