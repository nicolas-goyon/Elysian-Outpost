using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

//public partial struct VoxelPlacementSystem : ISystem {



//    [BurstCompile]
//    public void OnUpdate(ref SystemState state) { 
//        VoxelData[][] voxelDatas = VoxelControl.Instance.GetVoxelDatas();
//        EntityManager entityManager = state.EntityManager;    
//        VoxelConfigData config = SystemAPI.GetSingleton<VoxelConfigData>();
//        Entity voxelPrefab = config.voxelPrefab;

//        LocalTransform voxelTransform = entityManager.GetComponentData<LocalTransform>(voxelPrefab);
//        voxelTransform.Position.y = (voxelTransform.Scale / 2);
//        for (int x = 0; x < voxelDatas.Length; x++) {
//            for (int y = 0; y < voxelDatas[x].Length; y++) {
//                VoxelData voxelData = voxelDatas[x][y];
//                if (voxelData.type == 1) {
//                    Entity entity = entityManager.Instantiate(voxelPrefab);
//                    entityManager.AddComponentData(entity, new Voxel { color = voxelData.color });
//                    entityManager.AddComponentData(entity, new URPMaterialPropertyBaseColor { Value = ((float4) voxelData.color) / 255f });

//                    voxelTransform.Position.x = x;
//                    voxelTransform.Position.z = y;

//                    entityManager.SetComponentData(entity, voxelTransform);

//                }
//            }
//        }
//    }
//}
public partial class VoxelPlacementSystem : SystemBase {

    private EntityQuery query;

    protected override void OnCreate() {
        query = GetEntityQuery(ComponentType.ReadOnly<Voxel>());
    }

    private int numberOfVoxelPlaced = 0;

    [BurstCompile]
    protected override void OnUpdate() {
        if (VoxelControl.Instance.GetNumberOfVoxels() == numberOfVoxelPlaced) { 
            return;
        }
        
        if (numberOfVoxelPlaced > 0) {
            Clean();
        }
        Clean();
        Place();
    }

    private void Clean() {
        EntityManager entityManager = EntityManager;
        NativeArray<Entity> entities = query.ToEntityArray(Allocator.TempJob);
        entityManager.DestroyEntity(entities);
        entities.Dispose();
    }


    [BurstCompile]
    private void Place() {
        VoxelData[][] voxelDatas = VoxelControl.Instance.GetVoxelDatas();
        VoxelConfigData config = SystemAPI.GetSingleton<VoxelConfigData>();
        Entity voxelPrefab = config.voxelPrefab;

        LocalTransform voxelTransform = EntityManager.GetComponentData<LocalTransform>(voxelPrefab);
        voxelTransform.Position.y = (voxelTransform.Scale / 2);

        EntityCommandBuffer commandBuffer = new(Allocator.Temp);
        int numberOfVoxel = VoxelControl.Instance.GetNumberOfVoxels();
        numberOfVoxelPlaced = 0;
        for (int index = 0; index < numberOfVoxel; index++) { 
            int x = index % voxelDatas.Length;
            int y = index / voxelDatas.Length;
            VoxelData voxelData = voxelDatas[x][y];
            if (voxelData.type == 1) {

                Entity entity = commandBuffer.Instantiate(voxelPrefab);
                commandBuffer.AddComponent(entity, new Voxel { color = voxelData.color });
                commandBuffer.AddComponent(entity, new URPMaterialPropertyBaseColor { Value = ((float4)voxelData.color) / 255f });

                voxelTransform.Position.x = x;
                voxelTransform.Position.z = y;

                commandBuffer.SetComponent(entity, voxelTransform);
                numberOfVoxelPlaced++;

            }
        }
        commandBuffer.Playback(EntityManager);
        commandBuffer.Dispose();
    }
}