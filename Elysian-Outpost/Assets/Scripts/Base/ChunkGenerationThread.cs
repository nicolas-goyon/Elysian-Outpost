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
        private readonly ConcurrentQueue<(ExampleChunk chunk, ExampleMesh mesh)> _completedMeshes = new();
        private readonly AutoResetEvent _signal = new(false);
        private readonly Thread _thread;
        private volatile bool _running = true;

        public ChunkGenerationThread()
        {
            _thread = new Thread(ThreadLoop)
            {
                IsBackground = true,
                Name = "ChunkMeshGeneration"
            };
            _thread.Start();
        }

        public void EnqueueChunk(ExampleChunk chunk)
        {
            if (!_running)
            {
                throw new ObjectDisposedException(nameof(ChunkGenerationThread));
            }

            _pendingWorkers.Enqueue(new ChunkMeshGenerationWorker(chunk));
            _signal.Set();
        }

        public bool TryDequeueGeneratedMesh(out (ExampleChunk chunk, ExampleMesh mesh) result)
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
                    (ExampleChunk chunk, ExampleMesh mesh) = worker.Execute();
                    _completedMeshes.Enqueue((chunk, mesh));
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