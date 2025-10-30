using System;
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

    [SerializeField] private int _viewDistanceLoadingInChunks = 3;
    [SerializeField] private int _viewDistanceUnloadingInChunks = 5;
    [SerializeField] private GameInputs gameInputs;
    [SerializeField] private GameObject spherePrefab;
    private GameObject _sphere;

    private Vector3 _lastPosition;
    private TerrainHolder _terrainHolder;

    private void Start()
    {
        _terrainHolder = new TerrainHolder(_templateObject, _textureAtlas);
    }

    private bool isClicked = false;
    
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
                // Debug.Log("Hit " + hitInfo.collider.gameObject.name);
                Vector3 hitPoint = hitInfo.point;
                if (_sphere == null)
                {
                    _sphere = Instantiate(spherePrefab, hitPoint, Quaternion.identity);
                }
                _sphere.transform.position = hitPoint - hitInfo.normal * 0.1f;

                if (isClicked)
                {
                    return;
                }
                isClicked = true;
                
                (ExampleChunk chunk, uint3 voxelPosition) = _terrainHolder.GetHitChunkAndVoxelPositionAtRaycast(hitInfo);
                if (chunk == null)
                {
                    Debug.Log($"No chunk at {hitInfo}");
                    return;
                }
                Debug.Log($"Removing voxel at {voxelPosition} in chunk at {chunk.WorldPosition}");
                chunk.RemoveVoxel(voxelPosition);
                _terrainHolder.ReloadChunk(chunk);
            }
        }
        else
        {
            isClicked = false;
        }
            
        
        ProcessGeneratedChunks();

        List<int3> requiredChunks = GetChunksInViewDistance(PositionToInt(), _viewDistanceLoadingInChunks);
        foreach (int3 chunkPos in requiredChunks)
        {
            _terrainHolder.GenerateNewChunkAt(chunkPos);
            // TODO : Second thread need to be less greedy
        }
        List<int3> chunksToUnload = GetChunksToUnload();
        foreach (int3 pos in chunksToUnload)
        {
            _terrainHolder.UnLoadChunkAt(pos);
        }
    }
    

    private void OnDestroy()
    {
        _terrainHolder.Dispose();
    }

    #region Get Chunks to Load/Unload

    /// <summary>
    /// Checks which chunks are out of view distance and should be unloaded
    /// </summary>
    /// <returns>List of tuples containing chunk position and instance to be unloaded</returns>
    private List<int3> GetChunksToUnload()
    {
        HashSet<int3> currentViewChunks = new HashSet<int3>(GetChunksInViewDistance(PositionToInt(), _viewDistanceUnloadingInChunks));

        List<int3> chunksToUnload = new List<int3>();
        foreach ((int3 chunkPos, ExampleChunk chunkTask) in _terrainHolder.GetAllLoadedChunks())
        {
            if (!currentViewChunks.Contains(chunkPos))
            {
                chunksToUnload.Add(chunkPos);
            }
        }
        return chunksToUnload;
    }

    /// <summary>
    /// Returns all chunk positions within the view distance from a center position
    /// </summary>
    /// <param name="centerPosition">The center position to calculate chunks around</param>
    /// <returns>List of chunk positions (in chunk coordinates)</returns>
    private List<int3> GetChunksInViewDistance(int3 centerPosition, int viewDistance)
    {
        List<int3> chunks = new List<int3>();
        int centerChunkX = Mathf.FloorToInt(centerPosition.x / (float)_terrainHolder._chunkSize);
        int centerChunkZ = Mathf.FloorToInt(centerPosition.z / (float)_terrainHolder._chunkSize);

        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                // Calculate the chunk position
                int chunkX = centerChunkX + x;
                int chunkZ = centerChunkZ + z;

                // Only include chunks within the circular view distance
                float distance = Mathf.Sqrt(x * x + z * z);
                if (distance <= viewDistance)
                {
                    chunks.Add(new int3(
                        chunkX * _terrainHolder._chunkSize,
                        0, // Y is always 0 for chunk positions
                        chunkZ * _terrainHolder._chunkSize
                    ));
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
        _terrainHolder.InstanciateOneChunk();
    }

}