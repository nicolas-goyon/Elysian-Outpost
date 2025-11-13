using ScriptableObjectsDefinition;
using Unity.Mathematics;
using UnityEngine;

namespace Base.Terrain
{
    public class FixedSizeTerrain : MonoBehaviour
    {
        [SerializeField] private GameObject _templateObject;
        [SerializeField] private TextureAtlas _textureAtlas;

        [SerializeField] private int _terrainSize = 3;
        [SerializeField] private int _terrainHeight = 5;
        [SerializeField] private int _seed = 0;
        [SerializeField] private int3 _chunkSize = new int3(16, 16, 16);
        [SerializeField] private int _maxConcurrentWorkers = 10;
        [SerializeField] private int _meshPerFrame = 50;

        private TerrainHolder _terrainHolder;

        private bool _running = false;

        private void Start()
        {
            GameObject holder = new GameObject("ChunksHolder");
            _terrainHolder = new TerrainHolder(_templateObject, _textureAtlas, holder, _chunkSize, _seed, _maxConcurrentWorkers);
        }
    
        public void BegginGeneration()
        {
            int3[] requiredChunks = GetChunksInViewDistance(PositionToInt(), _terrainSize);
            foreach (int3 chunkPos in requiredChunks)
            {
                _terrainHolder.GenerateNewChunkAt(chunkPos);
            }

            _running = true;
        }

        public void Pause()
        {
            _running = false;
        }
    
        public void ClearAllChunks()
        {
            _terrainHolder.ClearAllChunks();
            _running = false;
        }
    
        public void Cleanup(){
        
            _terrainHolder.ClearAllChunks();
            _terrainHolder.Dispose();
            _running = false;
        }

        // Update is called once per frame
        private void Update()
        {
            if (_running)  ProcessGeneratedChunks();
        }
    

        private void OnDestroy()
        {
            _terrainHolder.Dispose();
        }

        #region Get Chunks to Load/Unload

        /// <summary>
        /// Returns all chunk positions within the view distance from a center position TODO : Lets say the view distance is square
        /// </summary>
        /// <param name="centerPosition">The center position to calculate chunks around</param>
        /// <param name="viewDistance">The view distance in chunks</param>
        /// <returns>List of chunk positions (in chunk coordinates)</returns>
        private int3[] GetChunksInViewDistance(int3 centerPosition, int viewDistance)
        {
            int chunksInViewDistance = (2 * viewDistance) * (2 * viewDistance) * (_terrainHeight);
            int3[] chunks = new int3[chunksInViewDistance];
            int centerPositionChunkX = Mathf.FloorToInt((float)centerPosition.x / _terrainHolder.ChunkSize.x);
            int centerPositionChunkZ = Mathf.FloorToInt((float)centerPosition.z / _terrainHolder.ChunkSize.z);
            int xStart = (centerPositionChunkX - viewDistance) * _terrainHolder.ChunkSize.x;
            int zStart = (centerPositionChunkZ - viewDistance)* _terrainHolder.ChunkSize.z;
            int xEnd = (centerPositionChunkX + viewDistance)* _terrainHolder.ChunkSize.x;
            int zEnd = (centerPositionChunkZ + viewDistance)* _terrainHolder.ChunkSize.z;
        
        
            int index = 0;
            for (int x = xStart; x < xEnd; x += _terrainHolder.ChunkSize.x)
            {
                for (int z = zStart; z < zEnd; z += _terrainHolder.ChunkSize.z)
                {
                    for (int y = 0; y < _terrainHeight * _terrainHolder.ChunkSize.y; y +=
                             _terrainHolder.ChunkSize.y)
                    {
                        chunks[index] = new int3(x, y, z);
                        index++;
                    }
                }
            }
            return chunks;
        }
    
        #endregion
    
    

        private int3 PositionToInt()
        {
            return new int3(Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.y), Mathf.FloorToInt(transform.position.z));
        }

    

        private void ProcessGeneratedChunks()
        {
            // _terrainHolder.InstanciateOneChunk();
            _terrainHolder.InstanciateMultipleChunks(_meshPerFrame);
        }
    }
}
