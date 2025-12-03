using System.Collections.Generic;
using System.Linq;
using Libs.VoxelMeshOptimizer;
using Libs.VoxelMeshOptimizer.Multithreading;
using ScriptableObjectsDefinition;
using TerrainGeneration;
using Unity.Mathematics;
using UnityEngine;
using Mesh = Libs.VoxelMeshOptimizer.Mesh;

namespace Base.Terrain
{
    /**
     * A class to hold all the chunks in the world.
     */
    public class TerrainHolder : System.IDisposable
    {
        private Dictionary<int3, (Chunk chunk, InstanciatedChunk gameObject)> Chunks { get; }

        public readonly int3 ChunkSize;
        private readonly MainGeneration _gen;
        private readonly ChunkGenerationThread _chunkGenerationThread;
        private readonly GameObject _templateObject;
        private readonly TextureAtlas _textureAtlas;

        private readonly GameObject _chunksHolder;

        public TerrainHolder(GameObject templateObject, TextureAtlas atlas, GameObject chunksHolder, int3 chunkSize,
            int seed, int maxConcurrentWorkers)
        {
            ChunkSize = chunkSize;
            _gen = new MainGeneration(ChunkSize, seed);
            _chunkGenerationThread = new ChunkGenerationThread(maxConcurrentWorkers);
            Chunks = new Dictionary<int3, (Chunk chunk, InstanciatedChunk gameObject)>();
            _templateObject = templateObject;
            _textureAtlas = atlas;
            _chunksHolder = chunksHolder;
        }


        #region GetChunkAndVoxelPosition

        /**
         * Given a raycast hit, return the chunk and voxel position the ray hit inside that chunk.
         */
        public (Chunk chunk, uint3 voxelPosition) GetHitChunkAndVoxelPositionAtRaycast(RaycastHit hitInfo)
        {
            // Slightly increase the hit point inside the voxel
            Vector3 hitPoint = hitInfo.point - hitInfo.normal * 0.01f;
            int3 chunkPos = new int3(
                Mathf.FloorToInt(hitPoint.x / ChunkSize.x) * ChunkSize.x,
                Mathf.FloorToInt(hitPoint.y / ChunkSize.y) * ChunkSize.y,
                Mathf.FloorToInt(hitPoint.z / ChunkSize.z) * ChunkSize.z
            );

            if (!Chunks.TryGetValue(chunkPos, out (Chunk chunk, InstanciatedChunk gameObject) chunkData))
            {
                return (null, new uint3(0, 0, 0)); // Chunk not loaded
            }

            uint3 localVoxelPos = new uint3(
                (uint)(Mathf.FloorToInt(hitPoint.x) - chunkPos.x),
                (uint)(Mathf.FloorToInt(hitPoint.y) - chunkPos.y),
                (uint)(Mathf.FloorToInt(hitPoint.z) - chunkPos.z)
            );
            return (chunkData.chunk, localVoxelPos);
        }

        /**
         * Given a raycast hit, return the chunk and voxel position just before the hit inside that chunk.
         *
         * In other words, the voxel that the ray was in before hitting the target voxel. (e.g. for placing a block against another block)
         */
        public (Chunk chunk, uint3 voxelPosition) GetChunkAndVoxelPositionBeforeHitRaycast(RaycastHit hitInfo)
        {
            // Slightly decrease the hit point inside the voxel
            Vector3 hitPoint = hitInfo.point + hitInfo.normal * 0.01f;
            int3 chunkPos = new int3(
                Mathf.FloorToInt(hitPoint.x / ChunkSize.x) * ChunkSize.x,
                Mathf.FloorToInt(hitPoint.y / ChunkSize.y) * ChunkSize.y,
                Mathf.FloorToInt(hitPoint.z / ChunkSize.z) * ChunkSize.z
            );

            if (!Chunks.TryGetValue(chunkPos, out (Chunk chunk, InstanciatedChunk gameObject) chunkData))
            {
                return (null, new uint3(0, 0, 0)); // Chunk not loaded
            }

            uint3 localVoxelPos = new uint3(
                (uint)(Mathf.FloorToInt(hitPoint.x) - chunkPos.x),
                (uint)(Mathf.FloorToInt(hitPoint.y) - chunkPos.y),
                (uint)(Mathf.FloorToInt(hitPoint.z) - chunkPos.z)
            );
            return (chunkData.chunk, localVoxelPos);
        }

        #endregion

        #region GenerateChunk

        public void GenerateNewChunkAt(int3 chunkPos)
        {
            if (Chunks.ContainsKey(chunkPos)) return;
            if (_chunkGenerationThread == null)
            {
                throw new System.Exception("Chunk generation thread is not initialized.");
            }

            _chunkGenerationThread.EnqueueChunk(chunkPos, int3 => new Chunk(_gen.GenerateChunkAt(chunkPos), chunkPos));
        }


        public void ReloadChunk(Chunk chunk)
        {
            if (_chunkGenerationThread == null)
                throw new System.Exception("Chunk generation thread is not initialized.");

            _chunkGenerationThread.EnqueueChunk(chunk.WorldPosition, int3 => chunk);
        }

        #endregion

        #region ProcessGeneratedChunks

        /**
         * Dequeue one generated chunk and instanciate it.
         */
        public bool InstanciateOneChunk(out InstanciatedChunk instanciatedChunk)
        {
            if (!TryGetGeneratedChunk(out (Chunk chunk, Mesh mesh) result))
            {
                instanciatedChunk = null;
                return false;
            }
            (Chunk chunk, Mesh mesh) = result;

            if (Chunks.TryGetValue(chunk.WorldPosition,
                    out (Chunk chunk, InstanciatedChunk instanciatedChunk) chunkData))
            {
                if (chunkData.instanciatedChunk is not null)
                {
                    GameObject.Destroy(chunkData.instanciatedChunk.gameObject);
                }

                Chunks[chunk.WorldPosition] = (chunk, null);
            }

            InstanciatedChunk chunkInstance = Create(mesh, chunk.WorldPosition);
            Chunks[chunk.WorldPosition] = (chunk, chunkInstance);
            instanciatedChunk = chunkInstance;
            return true;
        }

        private bool TryGetGeneratedChunk(out (Chunk chunk, Mesh mesh) result)
        {
            return _chunkGenerationThread.TryDequeueGeneratedMesh(out result);
        }


        private InstanciatedChunk Create(Mesh mesh, int3 position)
        {
            GameObject obj = GameObject.Instantiate(_templateObject, _chunksHolder.transform);
            InstanciatedChunk singleObject = obj.GetComponent<InstanciatedChunk>();
            obj.transform.position = new Vector3(position.x, position.y, position.z);

            singleObject.SetMesh(mesh, _textureAtlas);

            return singleObject;
        }

        #endregion

        #region Cleanup

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

        #endregion

        #region Interract With Chunks

        public Voxel GetVoxelAtWorldPosition(int3 worldPosition)
        {
            int3 chunkPos = new int3(
                Mathf.FloorToInt((float)worldPosition.x / ChunkSize.x) * ChunkSize.x,
                Mathf.FloorToInt((float)worldPosition.y / ChunkSize.y) * ChunkSize.y,
                Mathf.FloorToInt((float)worldPosition.z / ChunkSize.z) * ChunkSize.z
            );

            if (!Chunks.TryGetValue(chunkPos, out (Chunk chunk, InstanciatedChunk gameObject) chunkData))
            {
                return null; // Chunk not loaded
            }

            uint3 localVoxelPos = new uint3(
                (uint)(worldPosition.x - chunkPos.x),
                (uint)(worldPosition.y - chunkPos.y),
                (uint)(worldPosition.z - chunkPos.z)
            );
            return chunkData.chunk.Get(localVoxelPos.x, localVoxelPos.y, localVoxelPos.z);
        }

        public Chunk GetChunkAtWorldPosition(int3 worldPosition)
        {
            int3 chunkPos = new int3(
                Mathf.FloorToInt((float)worldPosition.x / ChunkSize.x) * ChunkSize.x,
                Mathf.FloorToInt((float)worldPosition.y / ChunkSize.y) * ChunkSize.y,
                Mathf.FloorToInt((float)worldPosition.z / ChunkSize.z) * ChunkSize.z
            );

            if (!Chunks.TryGetValue(chunkPos, out (Chunk chunk, InstanciatedChunk gameObject) chunkData))
            {
                return null; // Chunk not loaded
            }

            return chunkData.chunk;
        }

        #endregion

        public List<InstanciatedChunk> GetAllInstanciatedChunks()
        {
            return Chunks.Values.Where(v => v.gameObject != null).Select(v => v.gameObject).ToList();
        }
        
    }
}