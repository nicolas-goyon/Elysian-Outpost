using Base.AI;
using Base.InGameConsole;
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
        [SerializeField] private NavMeshHotReload _navMeshHotReload;
        [SerializeField] private int _voxelScale = 1;
        [SerializeField] private float3 TerrainCenterOffset = new float3(0,0,0);

        public TerrainHolder TerrainHolder { get; private set; }

        private bool _running = false;

        private void Start()
        {
            GameObject holder = new("ChunksHolder")
            {
                transform =
                {
                    parent = transform
                }
            };
            TerrainHolder = new TerrainHolder(_templateObject, _textureAtlas, holder, _chunkSize, _seed,
                _maxConcurrentWorkers);
            
            _navMeshHotReload.Init(this);
                
        }

        public void BegginGeneration()
        {
            int3[] requiredChunks = GetChunksInViewDistance(PositionToInt(), _terrainSize);
            foreach (int3 chunkPos in requiredChunks)
            {
                TerrainHolder.GenerateNewChunkAt(chunkPos);
            }

            _running = true;
        }

        public void Pause()
        {
            _running = !_running;
        }

        public void ClearAllChunks()
        {
            TerrainHolder.ClearAllChunks();
            _running = false;
        }

        public void Cleanup()
        {
            TerrainHolder.ClearAllChunks();
            TerrainHolder.Dispose();
            _running = false;
        }

        // Update is called once per frame
        private void Update()
        {
            if (_running) ProcessGeneratedChunks();
        }


        private void OnDestroy()
        {
            TerrainHolder.Dispose();
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
            int centerPositionChunkX = Mathf.FloorToInt((float)centerPosition.x / TerrainHolder.ChunkSize.x);
            int centerPositionChunkZ = Mathf.FloorToInt((float)centerPosition.z / TerrainHolder.ChunkSize.z);
            int xStart = (centerPositionChunkX - viewDistance) * TerrainHolder.ChunkSize.x;
            int zStart = (centerPositionChunkZ - viewDistance) * TerrainHolder.ChunkSize.z;
            int xEnd = (centerPositionChunkX + viewDistance) * TerrainHolder.ChunkSize.x;
            int zEnd = (centerPositionChunkZ + viewDistance) * TerrainHolder.ChunkSize.z;


            int index = 0;
            for (int x = xStart; x < xEnd; x += TerrainHolder.ChunkSize.x)
            {
                for (int z = zStart; z < zEnd; z += TerrainHolder.ChunkSize.z)
                {
                    for (int y = 0;
                         y < _terrainHeight * TerrainHolder.ChunkSize.y;
                         y +=
                             TerrainHolder.ChunkSize.y)
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
            return new int3(Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.y),
                Mathf.FloorToInt(transform.position.z));
        }


        private void ProcessGeneratedChunks()
        {
            bool anyMeshProcessed = false;
            while (TerrainHolder.InstanciateOneChunk(out InstanciatedChunk instanciatedChunk))
            {
                anyMeshProcessed = true;
            }
            if (anyMeshProcessed)
                _navMeshHotReload.HotReload(TerrainHolder);
        }


        public Vector3 GetWorldCenterPosition()
        {
            int3 centerPosition = PositionToInt();
            return new Vector3(
                centerPosition.x + TerrainCenterOffset.x,
                centerPosition.y + TerrainCenterOffset.y,
                centerPosition.z + TerrainCenterOffset.z
            );
        }

        public Vector3 GetWorldSize()
        {
            return new Vector3(
                _terrainSize * _chunkSize.x * _voxelScale,
                _terrainHeight * _chunkSize.y * _voxelScale,
                _terrainSize * _chunkSize.z * _voxelScale
            );
        }
    }
}