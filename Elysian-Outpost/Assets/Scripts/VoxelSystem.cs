using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

public partial struct VoxelSystem : ISystem {

    EntityQuery query;

    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<Voxel>();
        state.RequireForUpdate<NewVoxel>();
        // get Voxel entities that also are NewVoxel entities
        query = state.GetEntityQuery(typeof(Voxel), typeof(NewVoxel));
    }

    public void OnUpdate(ref SystemState state) {
        EntityManager entityManager = state.EntityManager;

        // get the native array of entities
        NativeArray<Entity> entities = query.ToEntityArray(Allocator.TempJob);

        // iterate over the entities
        for (int i = 0; i < entities.Length; i++) {
            Entity entity = entities[i];

            //// Change the color of the object based on the voxel's color
            Voxel voxel = entityManager.GetComponentData<Voxel>(entity);
            //NewVoxel newVoxel = entityManager.GetComponentData<NewVoxel>(entity);
            //URPMaterialPropertyBaseColor materialPropertyBaseColor = entityManager.GetComponentData<URPMaterialPropertyBaseColor>(entity);

            // set the color of the material
            //materialPropertyBaseColor.Value = voxel.color;
            //Debug.Log("Color: " + voxel.color);

            // remove the NewVoxel component
            entityManager.RemoveComponent<NewVoxel>(entity);
        }



    }
}
