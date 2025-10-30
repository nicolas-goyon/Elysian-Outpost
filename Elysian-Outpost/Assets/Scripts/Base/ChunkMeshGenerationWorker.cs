using System;
using System.Collections.Generic;
using Libs.VoxelMeshOptimizer;
using Libs.VoxelMeshOptimizer.OptimizationAlgorithms.DisjointSet;
using Unity.Mathematics;

namespace Base
{
    public readonly struct ChunkMeshGenerationWorker
    {
        private readonly ExampleChunk _chunk;

        public ChunkMeshGenerationWorker(ExampleChunk chunk)
        {
            _chunk = chunk;
        }

        public (ExampleChunk chunk, ExampleMesh) Execute()
        {
            DisjointSetMeshOptimizer<ExampleMesh> optimizer = new(new ExampleMesh(new List<MeshQuad>()));
            return (_chunk, optimizer.Optimize(_chunk));
        }
    }
}