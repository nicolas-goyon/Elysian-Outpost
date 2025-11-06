using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
    [SerializeField] private int _amountOfChunksInHeight = 5;
    [SerializeField] private GameInputs gameInputs;
    [SerializeField] private GameObject spherePrefab;
    private GameObject _sphere;

    private Vector3 _lastPosition;
    private TerrainHolder _terrainHolder; // TODO : Link to existing TerrainHolder

    private void Start()
    {
        // _terrainHolder = new TerrainHolder(_templateObject, _textureAtlas);
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
                    // Debug.Log($"No chunk at {hitInfo}");
                    return;
                }
                // Debug.Log($"Removing voxel at {voxelPosition} in chunk at {chunk.WorldPosition}");
                chunk.RemoveVoxel(voxelPosition);
                _terrainHolder.ReloadChunk(chunk);
            }
        }
        else
        {
            isClicked = false;
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
    /// Returns all chunk positions within the view distance from a center position TODO : Lets say the view distance is square
    /// </summary>
    /// <param name="centerPosition">The center position to calculate chunks around</param>
    /// <param name="viewDistance">The view distance in chunks</param>
    /// <returns>List of chunk positions (in chunk coordinates)</returns>
    private int3[] GetChunksInViewDistance(int3 centerPosition, int viewDistance)
    {
        int chunksInViewDistance = (2 * viewDistance) * (2 * viewDistance) * (_amountOfChunksInHeight);
        int3[] chunks = new int3[chunksInViewDistance];
        int centerPositionChunkX = Mathf.FloorToInt((float)centerPosition.x / _terrainHolder._chunkSize.x);
        int centerPositionChunkZ = Mathf.FloorToInt((float)centerPosition.z / _terrainHolder._chunkSize.z);
        int xStart = (centerPositionChunkX - viewDistance) * _terrainHolder._chunkSize.x;
        int zStart = (centerPositionChunkZ - viewDistance)* _terrainHolder._chunkSize.z;
        int xEnd = (centerPositionChunkX + viewDistance)* _terrainHolder._chunkSize.x;
        int zEnd = (centerPositionChunkZ + viewDistance)* _terrainHolder._chunkSize.z;
        
        
        int index = 0;
        for (int x = xStart; x < xEnd; x += _terrainHolder._chunkSize.x)
        {
            for (int z = zStart; z < zEnd; z += _terrainHolder._chunkSize.z)
            {
                for (int y = 0; y < _amountOfChunksInHeight * _terrainHolder._chunkSize.y; y +=
                         _terrainHolder._chunkSize.y)
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
        _terrainHolder.InstanciateOneChunk();
    }

}