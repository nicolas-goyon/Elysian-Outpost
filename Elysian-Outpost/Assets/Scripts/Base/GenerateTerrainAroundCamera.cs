using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Base;
using ScriptableObjectsDefinition;
using Unity.Mathematics;
using UnityEngine;

public class GenerateTerrainAroundCamera : MonoBehaviour
{
    [SerializeField] private GameObject _templateObject;
    [SerializeField] private TextureAtlas _textureAtlas;

    [SerializeField] private int _chunkSize = 50;
    [SerializeField] private int _viewDistanceInChunks = 3;
    [SerializeField] private GameInputs gameInputs;
    [SerializeField] private GameObject spherePrefab;
    private GameObject _sphere;
    
    private readonly Dictionary<int3, InstanciatedChunk> _loadedChunks = new();
    private readonly HashSet<int3> _chunksAwaitingGeneration = new();

    private Vector3 _lastPosition;

    private MainGeneration _gen;
    private ChunkGenerationThread _chunkGenerationThread;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        _gen = new MainGeneration(_chunkSize,123);
        _chunkGenerationThread = new ChunkGenerationThread(_gen);
    }

    // Update is called once per frame
    private void Update()
    {
        // Raycast and add a sphere at the hit point on left click
        if (gameInputs.IsLeftClick())
        {
            // Center of the screen
            Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
            if (Physics.Raycast(ray, out RaycastHit hitInfo))
            {
                Debug.Log("Hit " + hitInfo.collider.gameObject.name);
                Vector3 hitPoint = hitInfo.point;
                if (_sphere == null)
                {
                    _sphere = Instantiate(spherePrefab, hitPoint, Quaternion.identity);
                }
                _sphere.transform.position = hitPoint;
                
                (ExampleChunk chunk, uint3 voxelPosition) = GetHitChunkAndVoxelPositionAtRaycast(hitInfo);
                chunk?.RemoveVoxel(voxelPosition);
            }
        }
            
        
        ProcessGeneratedChunks();

        List<int3> requiredChunks = GetChunksInViewDistance(PositionToInt());
        foreach (int3 chunkPos in requiredChunks.Where(chunkPos => !_loadedChunks.ContainsKey(chunkPos) && !_chunksAwaitingGeneration.Contains(chunkPos)))
        {
            QueueChunk(chunkPos);
            break;
        }
        List<(int3, InstanciatedChunk)> chunksToUnload = GetChunksToUnload();
        foreach ((int3 pos, InstanciatedChunk obj) in chunksToUnload)
        {
            _loadedChunks.Remove(pos);
            Destroy(obj);
        }
    }
    
    private (ExampleChunk chunk, uint3 voxelPosition) GetHitChunkAndVoxelPositionAtRaycast(RaycastHit hitInfo)
    {
        Vector3 hitPoint = hitInfo.point;
        int3 chunkPos = new int3(
            Mathf.FloorToInt(hitPoint.x / _chunkSize) * _chunkSize,
            0,
            Mathf.FloorToInt(hitPoint.z / _chunkSize) * _chunkSize
        );

        if (!_loadedChunks.TryGetValue(chunkPos, out InstanciatedChunk chunkInstance)) return (null, new uint3(0, 0, 0));
        ExampleChunk chunk = chunkInstance.chunk;
        if (chunk == null) return (null, new uint3(0, 0, 0));
        
        uint3 localVoxelPos = new uint3(
            (uint)(Mathf.FloorToInt(hitPoint.x) - chunkPos.x),
            (uint)(Mathf.FloorToInt(hitPoint.y ) - chunkPos.y),
            (uint)(Mathf.FloorToInt(hitPoint.z) - chunkPos.z)
        );
        return (chunk, localVoxelPos);

    }

    private void OnDestroy()
    {
        _chunkGenerationThread?.Dispose();
    }

    #region Get Chunks to Load/Unload

    /// <summary>
    /// Checks which chunks are out of view distance and should be unloaded
    /// </summary>
    /// <returns>List of tuples containing chunk position and instance to be unloaded</returns>
    private List<(int3, InstanciatedChunk)> GetChunksToUnload()
    {
        HashSet<int3> currentViewChunks = new HashSet<int3>(GetChunksInViewDistance(PositionToInt()));

        List<(int3, InstanciatedChunk)> chunksToUnload = new List<(int3, InstanciatedChunk)>();
        foreach ((int3 chunkPos, InstanciatedChunk chunkTask) in _loadedChunks.ToList())
        {
            if (!currentViewChunks.Contains(chunkPos))
            {
                chunksToUnload.Add((chunkPos, chunkTask));
            }
        }
        return chunksToUnload;
    }

    /// <summary>
    /// Returns all chunk positions within the view distance from a center position
    /// </summary>
    /// <param name="centerPosition">The center position to calculate chunks around</param>
    /// <returns>List of chunk positions (in chunk coordinates)</returns>
    private List<int3> GetChunksInViewDistance(int3 centerPosition)
    {
        List<int3> chunks = new List<int3>();
        int centerChunkX = Mathf.FloorToInt(centerPosition.x / (float)_chunkSize);
        int centerChunkZ = Mathf.FloorToInt(centerPosition.z / (float)_chunkSize);

        for (int x = -_viewDistanceInChunks; x <= _viewDistanceInChunks; x++)
        {
            for (int z = -_viewDistanceInChunks; z <= _viewDistanceInChunks; z++)
            {
                // Calculate the chunk position
                int chunkX = centerChunkX + x;
                int chunkZ = centerChunkZ + z;

                // Only include chunks within the circular view distance
                float distance = Mathf.Sqrt(x * x + z * z);
                if (distance <= _viewDistanceInChunks)
                {
                    chunks.Add(new int3(
                        chunkX * _chunkSize,
                        0, // Y is always 0 for chunk positions
                        chunkZ * _chunkSize
                    ));
                }
            }
        }

        return chunks;
    }
    // Use raycasting to find the chunk at the camera's position
    private (ExampleChunk chunk, int3 position) GetChunkAtCamera()
    {
        Ray ray = new Ray(transform.position + Vector3.up * 1000, Vector3.down);
        if (!Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity)) return (null, new int3(0, 0, 0));
        Vector3 hitPoint = hitInfo.point;
        int3 chunkPos = new int3(
            Mathf.FloorToInt(hitPoint.x / _chunkSize) * _chunkSize,
            0,
            Mathf.FloorToInt(hitPoint.z / _chunkSize) * _chunkSize
        );

        if (_loadedChunks.TryGetValue(chunkPos, out InstanciatedChunk chunkInstance))
        {
            return (chunkInstance.chunk, chunkPos);
        }

        return (null, new int3(0, 0, 0));
    }
    
    #endregion
    
    
    private void QueueChunk(int3 chunkPos)
    {
        if (_chunkGenerationThread == null)
        {
            return;
        }

        if (_chunksAwaitingGeneration.Add(chunkPos))
        {
            _chunkGenerationThread.EnqueueChunk(chunkPos);
        }
    }

    private int3 PositionToInt()
    {
        return new int3(Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.y), Mathf.FloorToInt(transform.position.z));
    }

    
    
    private InstanciatedChunk Create(ExampleMesh mesh, int3 position)
    {
        GameObject obj = Instantiate(_templateObject);
        InstanciatedChunk singleObject = obj.GetComponent<InstanciatedChunk>();
        obj.transform.position = new Vector3(position.x, position.y, position.z);

        singleObject.SetMesh(mesh, _textureAtlas);

        return singleObject;
    }

    private void ProcessGeneratedChunks()
    {
        if (_chunkGenerationThread == null)
        {
            return;
        }

        while (_chunkGenerationThread.TryDequeueGeneratedMesh(out var result))
        {
            (int3 position, ExampleChunk chunk, ExampleMesh mesh) = result;
            _chunksAwaitingGeneration.Remove(position);

            if (!IsChunkWithinViewDistance(position))
            {
                continue;
            }

            if (_loadedChunks.ContainsKey(position))
            {
                continue;
            }

            InstanciatedChunk chunkInstance = Create(mesh, position);
            _loadedChunks[position] = chunkInstance;
            _loadedChunks[position].chunk = chunk;
        }
    }

    private bool IsChunkWithinViewDistance(int3 chunkPosition)
    {
        int3 centerPosition = PositionToInt();
        int centerChunkX = Mathf.FloorToInt(centerPosition.x / (float)_chunkSize);
        int centerChunkZ = Mathf.FloorToInt(centerPosition.z / (float)_chunkSize);

        int chunkX = Mathf.RoundToInt(chunkPosition.x / (float)_chunkSize);
        int chunkZ = Mathf.RoundToInt(chunkPosition.z / (float)_chunkSize);

        int deltaX = chunkX - centerChunkX;
        int deltaZ = chunkZ - centerChunkZ;

        float distance = Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        return distance <= _viewDistanceInChunks;
    }
}