using System;
using System.Collections.Generic;
using Libs.VoxelMeshOptimizer;
using Libs.VoxelMeshOptimizer.OptimizationAlgorithms.DisjointSet;
using Unity.Mathematics;

namespace Base
{
    public readonly struct ChunkMeshGenerationWorker
    {
        private readonly MainGeneration _generator;

        public int3 ChunkPosition { get; }

        public ChunkMeshGenerationWorker(MainGeneration generator, int3 chunkPosition)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            ChunkPosition = chunkPosition;
        }

        public (ExampleChunk chunk, ExampleMesh) Execute()
        {
            ExampleChunk chunk = new ExampleChunk(_generator.GenerateChunkAt(ChunkPosition.x, ChunkPosition.z));
            DisjointSetMeshOptimizer<ExampleMesh> optimizer = new(new ExampleMesh(new List<MeshQuad>()));
            return (chunk, optimizer.Optimize(chunk));
        }
    }
}