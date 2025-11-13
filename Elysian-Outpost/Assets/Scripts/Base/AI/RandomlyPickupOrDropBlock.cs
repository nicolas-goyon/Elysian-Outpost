using Base.Terrain;
using Libs.VoxelMeshOptimizer;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(WanderingEntity))]
public class RandomlyPickupOrDropBlock : MonoBehaviour
{
    /**
     * Chance per second to attempt to pick up or drop a block in percentage (0.1 = 10% chance per second)
     */
    [SerializeField] private float _actionChancePerSecond = 0.1f; 
    [SerializeField] private float _actionRadius = 5.0f;
    
    private WanderingEntity _wanderingEntity;
    private FixedSizeTerrain _terrain;
    
    private short _handlingInventoryVoxelID = -1;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _wanderingEntity = GetComponent<WanderingEntity>();
        _terrain = FindObjectOfType<FixedSizeTerrain>();
    }
    
    // Update is called once per frame
    void Update()
    {
        if (_wanderingEntity._currentState != WanderingEntity.WanderState.Idle) return;
        
        float chanceThisFrame = _actionChancePerSecond * Time.deltaTime;
        if (Random.value > chanceThisFrame) return;
        
        Vector3 randomDirection = Random.insideUnitSphere * _actionRadius;
        
        Ray ray = new Ray(transform.position, randomDirection);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, _actionRadius))
        {
            if (_handlingInventoryVoxelID < 0)
            {
                (Chunk chunk, uint3 voxelPosition)  = _terrain.TerrainHolder.GetChunkAndVoxelPositionBeforeHitRaycast(hitInfo);
                Voxel voxelData = chunk.Get(voxelPosition.x, voxelPosition.y, voxelPosition.z);
                if (voxelData == null || voxelData.IsSolid)
                {
                    Debug.LogError($"Should not happen: hit a solid voxel at {voxelPosition} in chunk at {chunk.WorldPosition}");
                    return;
                }
                // Place the voxel
                chunk.Set(voxelPosition.x, voxelPosition.y, voxelPosition.z, new Voxel((ushort)_handlingInventoryVoxelID));
            }
            else
            {
                (Chunk chunk, uint3 voxelPosition)  = _terrain.TerrainHolder.GetHitChunkAndVoxelPositionAtRaycast(hitInfo);
                Voxel voxelData = chunk.Get(voxelPosition.x, voxelPosition.y, voxelPosition.z);
                if (voxelData == null || !voxelData.IsSolid)
                {
                    Debug.LogError($"Should not happen: hit a non-solid voxel at {voxelPosition} in chunk at {chunk.WorldPosition}");
                    return;
                }
                
                // Pick up the voxel
                _handlingInventoryVoxelID = (short)voxelData.ID;
                chunk.Set(voxelPosition.x, voxelPosition.y, voxelPosition.z, null);
            }
            
            
        }
        else
        {
            Debug.Log("Ray did not hit anything.");
        }
        
        
    }

}
