using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;


[BurstCompile]
public partial class VoxelPlacementSystem : SystemBase {

    [BurstCompile]
    protected override void OnUpdate() {

        if (VoxelControl.Instance.IsQueueEmpty()) {
            return;
        }

        PlaceAll();


    }

    [BurstCompile]
    private void PlaceAll() {
        EntityCommandBuffer commandBuffer = new(Allocator.Temp);

        //int jobBatchSize = 100; // TODO : Maybe make this a config value & make it a job

        VoxelConfigData config = SystemAPI.GetSingleton<VoxelConfigData>();
        Entity voxelPrefab = config.voxelPrefab;

        while( VoxelControl.Instance.TryDequeue(out VoxelDataPosition voxel)) {
            float3 position = new(voxel.x, voxel.y, voxel.z);
            CreateVoxel(voxel.color, commandBuffer, voxelPrefab, position);
            
        }

        commandBuffer.Playback(EntityManager);
        commandBuffer.Dispose();
    }





    [BurstCompile]
    private Entity CreateVoxel(int4 color,  EntityCommandBuffer commandBuffer, Entity voxelPrefab, float3 position) {
        LocalTransform voxelTransform = new() {
            Position = position,
            Rotation = quaternion.identity,
            Scale = 1
        };

        Entity entity = commandBuffer.Instantiate(voxelPrefab);
        commandBuffer.AddComponent(entity, new VoxelInfo { color = color });
        commandBuffer.AddComponent(entity, new URPMaterialPropertyBaseColor { Value = ((float4)color) / 255f });


        commandBuffer.SetComponent(entity, voxelTransform);

        return entity;
    }

}
