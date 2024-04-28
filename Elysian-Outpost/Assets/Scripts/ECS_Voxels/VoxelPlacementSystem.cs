using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.Rendering;
using Global_Voxels;


[BurstCompile]
public partial class VoxelPlacementSystem : SystemBase {

    [BurstCompile]
    protected override void OnUpdate() {

        //if (VoxelControl.Instance.IsQueueEmpty()) {
        //    return;
        //}

        //PlaceAll();


    }

    [BurstCompile]
    private void PlaceAll() {
        EntityCommandBuffer commandBuffer = new(Allocator.Temp);

        //int jobBatchSize = 100; // TODO : Maybe make this a config value & make it a job

        VoxelConfigData config = SystemAPI.GetSingleton<VoxelConfigData>();
        Entity voxelPrefab = config.voxelPrefab;

        VoxelsHolder voxelsHolder = SystemAPI.GetSingleton<VoxelsHolder>();
        ref BlobArray<VoxelType> voxelTypes = ref voxelsHolder.BlobAssetReference.Value.voxels;

        if (!VoxelControl.Instance.TryDequeue(out VoxelChunkData chunk)) {
            return;
        }

        for (int i = 0; i < chunk.GetIndex(); i++) {
            CreateVoxel(chunk, commandBuffer, ref voxelTypes, voxelPrefab, i);
        }

        chunk.Dispose();


        commandBuffer.Playback(EntityManager);
        commandBuffer.Dispose();
    }


    [BurstCompile]
    private void CreateVoxel(VoxelChunkData chunk, EntityCommandBuffer commandBuffer, ref BlobArray<VoxelType> voxelTypes, Entity voxelPrefab, int index) { 
        Voxel newVoxel = chunk.GetVoxel(index);
        float3 position = chunk.GetVoxelPosition(index);

        LocalTransform voxelTransform = new() {
            Position = position,
            Rotation = quaternion.identity,
            Scale = 1
        };

        Entity entity = commandBuffer.Instantiate(voxelPrefab);
        commandBuffer.AddComponent(entity, newVoxel);
        commandBuffer.AddComponent(entity, new URPMaterialPropertyBaseColor { Value = voxelTypes[(int)newVoxel.kind].floatColor });

        commandBuffer.SetComponent(entity, voxelTransform);
    }





}
