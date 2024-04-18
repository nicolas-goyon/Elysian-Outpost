using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

public class NewVoxelAuthoring : MonoBehaviour
{

    public class Baker : Baker<NewVoxelAuthoring> {
        public override void Bake(NewVoxelAuthoring authoring) {
            Entity entity = GetEntity(TransformUsageFlags.Renderable);
            AddComponent(entity, new NewVoxel());
        }
    }
}

public struct NewVoxel : IComponentData {}