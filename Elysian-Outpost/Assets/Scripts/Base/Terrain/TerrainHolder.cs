using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ScriptableObjectsDefinition;
using TerrainGeneration;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

namespace Base
{
    /**
     * A class to hold all the chunks in the world.
     */
    public class TerrainHolder : System.IDisposable
    {
        private Dictionary<int3, (ExampleChunk chunk, InstanciatedChunk gameObject)> Chunks { get; }

        public readonly int3 ChunkSize;
        private readonly MainGeneration _gen;
        private readonly ChunkGenerationThread _chunkGenerationThread;
        private readonly GameObject _templateObject;
        private readonly TextureAtlas _textureAtlas;

        private readonly GameObject _chunksHolder;

        public TerrainHolder(GameObject templateObject, TextureAtlas atlas, GameObject chunksHolder, int3 chunkSize, int seed, int maxConcurrentWorkers)
        {
            ChunkSize = chunkSize;
            _gen = new MainGeneration(ChunkSize,seed);
            _chunkGenerationThread = new ChunkGenerationThread(maxConcurrentWorkers);
            Chunks = new Dictionary<int3, (ExampleChunk chunk, InstanciatedChunk gameObject)>();
            _templateObject = templateObject;
            _textureAtlas = atlas;
            _chunksHolder = chunksHolder;
        }
        
        ~TerrainHolder()
        {
            Dispose();
        }
        
        public void Dispose()
        {
            // Get a timer to "benchmark" how long the disposal takes
            
            _chunkGenerationThread.Dispose();
            Chunks.Clear();
        }
        
        public (ExampleChunk chunk, uint3 voxelPosition) GetHitChunkAndVoxelPositionAtRaycast(RaycastHit hitInfo)
        {
            // Slightly increase the hit point inside the voxel
            Vector3 hitPoint = hitInfo.point - hitInfo.normal * 0.01f;
            int3 chunkPos = new int3(
                Mathf.FloorToInt(hitPoint.x / ChunkSize.x) * ChunkSize.x,
                Mathf.FloorToInt(hitPoint.y / ChunkSize.y) * ChunkSize.y,
                Mathf.FloorToInt(hitPoint.z / ChunkSize.z) * ChunkSize.z
            );

            if (!Chunks.TryGetValue(chunkPos, out (ExampleChunk chunk, InstanciatedChunk gameObject) chunkData))
            {
                return (null, new uint3(0,0,0)); // Chunk not loaded
            }

            uint3 localVoxelPos = new uint3(
                (uint)(Mathf.FloorToInt(hitPoint.x) - chunkPos.x),
                (uint)(Mathf.FloorToInt(hitPoint.y ) - chunkPos.y),
                (uint)(Mathf.FloorToInt(hitPoint.z) - chunkPos.z)
            );
            return (chunkData.chunk, localVoxelPos);
        }
        
        public void GenerateNewChunkAt(int3 chunkPos)
        {
            if (Chunks.ContainsKey(chunkPos)) return;
            if (_chunkGenerationThread == null)
            {
                throw new System.Exception("Chunk generation thread is not initialized.");
            }
            
            _chunkGenerationThread.EnqueueChunk(chunkPos, int3 => new ExampleChunk(_gen.GenerateChunkAt(chunkPos),chunkPos));
        }
        
        
        // public void ReloadChunk(ExampleChunk chunk)
        // {
        //     if (_chunkGenerationThread == null) throw new System.Exception("Chunk generation thread is not initialized.");
        //     
        //     _chunkGenerationThread.EnqueueChunk(chunk.WorldPosition , int3 => chunk);
        // }

        private bool TryGetGeneratedChunk(out (ExampleChunk chunk, ExampleMesh mesh) result)
        {
            return _chunkGenerationThread.TryDequeueGeneratedMesh(out result);
        }
        
        
        /**
         * Dequeue one generated chunk and instanciate it.
         */
        public void InstanciateOneChunk()
        {
            if (!TryGetGeneratedChunk(out (ExampleChunk chunk, ExampleMesh mesh) result)) return;
            (ExampleChunk chunk, ExampleMesh mesh) = result;

            if (Chunks.TryGetValue(chunk.WorldPosition, out (ExampleChunk chunk, InstanciatedChunk instanciatedChunk) chunkData))
            {
                if (chunkData.instanciatedChunk is not null)
                {
                    GameObject.Destroy(chunkData.instanciatedChunk.gameObject);
                }
                Chunks[chunk.WorldPosition] = (chunk, null);
            }
                
            InstanciatedChunk chunkInstance = Create(mesh, chunk.WorldPosition);
            Chunks[chunk.WorldPosition] = (chunk, chunkInstance);
        }
        
        /**
         * Dequeue multiple generated chunks and instanciate them.
         */
        public void InstanciateMultipleChunks(int count)
        {
            for (int i = 0; i < count; i++)
            {
                InstanciateOneChunk();
            }
        }

        private InstanciatedChunk Create(ExampleMesh mesh, int3 position) 
        {
            GameObject obj = GameObject.Instantiate(_templateObject, _chunksHolder.transform);
            InstanciatedChunk singleObject = obj.GetComponent<InstanciatedChunk>();
            obj.transform.position = new Vector3(position.x, position.y, position.z);

            singleObject.SetMesh(mesh, _textureAtlas);

            return singleObject;
        }


        public IEnumerable<(int3 position, ExampleChunk chunk)> GetAllLoadedChunks()
        {
            return Chunks.Where(kvp => kvp.Value.gameObject is not null)
                .Select(kvp => (kvp.Key, kvp.Value.chunk));
        }

        public void ClearAllChunks()
        {
            foreach (var kvp in Chunks)
            {
                if (kvp.Value.gameObject is not null)
                {
                    GameObject.Destroy(kvp.Value.gameObject.gameObject);
                }
            }
            Chunks.Clear();
        }
    }
}