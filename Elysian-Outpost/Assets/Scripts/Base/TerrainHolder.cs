using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ScriptableObjectsDefinition;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

namespace Base
{
    /**
     * A class to hold all the chunks in the world.
     */
    public class TerrainHolder
    {
        private Dictionary<int3, (ExampleChunk chunk, InstanciatedChunk gameObject)> _chunks { get; }
        
        public readonly int _chunkSize = 50;
        private readonly MainGeneration _gen;
        private readonly ChunkGenerationThread _chunkGenerationThread;
        private readonly GameObject _templateObject;
        private readonly TextureAtlas _textureAtlas;

        public TerrainHolder(GameObject templateObject, TextureAtlas atlas)
        {
            _gen = new MainGeneration(_chunkSize,123); // TODO : Input chunk size and seed from outside
            _chunkGenerationThread = new ChunkGenerationThread();
            _chunks = new Dictionary<int3, (ExampleChunk chunk, InstanciatedChunk gameObject)>();
            _templateObject = templateObject;
            _textureAtlas = atlas;
        }
        
        ~TerrainHolder()
        {
            Dispose();
        }
        
        public void Dispose()
        {
            _chunkGenerationThread.Dispose();
            // Cleanup chunks structure
            _chunks.Clear();
        }
        
        public (ExampleChunk chunk, uint3 voxelPosition) GetHitChunkAndVoxelPositionAtRaycast(RaycastHit hitInfo)
        {
            Vector3 hitPoint = hitInfo.point;
            int3 chunkPos = new int3(
                Mathf.FloorToInt(hitPoint.x / _chunkSize) * _chunkSize,
                0,
                Mathf.FloorToInt(hitPoint.z / _chunkSize) * _chunkSize
            );

            if (!_chunks.TryGetValue(chunkPos, out (ExampleChunk chunk, InstanciatedChunk gameObject) chunkData))
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
            if (_chunks.ContainsKey(chunkPos)) return;
            if (_chunkGenerationThread == null)
            {
                throw new System.Exception("Chunk generation thread is not initialized.");
            }

            _chunks.Add(chunkPos, (new ExampleChunk(_gen.GenerateChunkAt(chunkPos.x, chunkPos.z),chunkPos), null));
            _chunkGenerationThread.EnqueueChunk(_chunks[chunkPos].chunk);
        }
        
        public void LoadChunk(ExampleChunk chunk)
        {
            if (_chunks.ContainsKey(chunk.WorldPosition)) return;
            if (_chunkGenerationThread == null)
            {
                throw new System.Exception("Chunk generation thread is not initialized.");
            }
            _chunks.Add(chunk.WorldPosition, (chunk, null));
            _chunkGenerationThread.EnqueueChunk(chunk);
        }

        public bool TryGetGeneratedChunk(out (ExampleChunk chunk, ExampleMesh mesh) result)
        {
            return _chunkGenerationThread.TryDequeueGeneratedMesh(out result);
        }
        
        // Process a hot reload of a chunk at the given position
        public void ReloadChunkAt(int3 chunkPos)
        {
        }
        
        public void UnLoadChunkAt(int3 chunkPos)
        {
            if (!_chunks.ContainsKey(chunkPos)) return;
            (ExampleChunk chunk, InstanciatedChunk instanciatedChunk) = _chunks[chunkPos];
            GameObject.Destroy(instanciatedChunk.gameObject);
            _chunks.Remove(chunkPos);
        }

        /**
         * Dequeue one generated chunk and instanciate it.
         */
        public void InstanciateOneChunk()
        {
            if (!TryGetGeneratedChunk(out (ExampleChunk chunk, ExampleMesh mesh) result)) return;
            (ExampleChunk chunk, ExampleMesh mesh) = result;

            if (_chunks.TryGetValue(chunk.WorldPosition, out (ExampleChunk chunk, InstanciatedChunk instanciatedChunk) chunkData))
            {
                if (chunkData.instanciatedChunk != null)
                {
                    GameObject.Destroy(chunkData.instanciatedChunk.gameObject);
                }
                _chunks[chunk.WorldPosition] = (chunk, null);
            }
                
            InstanciatedChunk chunkInstance = Create(mesh, chunk.WorldPosition);
            _chunks[chunk.WorldPosition] = (chunk, chunkInstance);

        }

        private InstanciatedChunk Create(ExampleMesh mesh, int3 position) 
        {
            GameObject obj = GameObject.Instantiate(_templateObject);
            InstanciatedChunk singleObject = obj.GetComponent<InstanciatedChunk>();
            obj.transform.position = new Vector3(position.x, position.y, position.z);

            singleObject.SetMesh(mesh, _textureAtlas);

            return singleObject;
        }


        public IEnumerable<(int3 position, ExampleChunk chunk)> GetAllLoadedChunks()
        {
            return _chunks.Where(kvp => kvp.Value.gameObject is not null)
                .Select(kvp => (kvp.Key, kvp.Value.chunk));
        }
    }
}