using System;
using System.Collections.Concurrent;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;

namespace Base
{
    public sealed class ChunkGenerationThread : IDisposable
    {
        private readonly ConcurrentQueue<ChunkMeshGenerationWorker> _pendingWorkers = new();
        private readonly ConcurrentQueue<(int3 position, ExampleMesh mesh)> _completedMeshes = new();
        private readonly AutoResetEvent _signal = new(false);
        private readonly Thread _thread;
        private readonly MainGeneration _generator;
        private volatile bool _running = true;

        public ChunkGenerationThread(MainGeneration generator)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _thread = new Thread(ThreadLoop)
            {
                IsBackground = true,
                Name = "ChunkMeshGeneration"
            };
            _thread.Start();
        }

        public void EnqueueChunk(int3 chunkPosition)
        {
            if (!_running)
            {
                throw new ObjectDisposedException(nameof(ChunkGenerationThread));
            }

            _pendingWorkers.Enqueue(new ChunkMeshGenerationWorker(_generator, chunkPosition));
            _signal.Set();
        }

        public bool TryDequeueGeneratedMesh(out (int3 position, ExampleMesh mesh) result)
        {
            return _completedMeshes.TryDequeue(out result);
        }

        private void ThreadLoop()
        {
            while (true)
            {
                if (!_pendingWorkers.TryDequeue(out ChunkMeshGenerationWorker worker))
                {
                    _signal.WaitOne();
                    if (!_running && _pendingWorkers.IsEmpty)
                    {
                        return;
                    }
                    continue;
                }

                try
                {
                    ExampleMesh mesh = worker.Execute();
                    _completedMeshes.Enqueue((worker.ChunkPosition, mesh));
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Chunk generation failed: {ex}");
                }
            }
        }

        public void Dispose()
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            _signal.Set();
            _thread.Join();
            _signal.Dispose();
        }
    }
}