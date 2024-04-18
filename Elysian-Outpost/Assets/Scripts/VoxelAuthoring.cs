using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

public class VoxelAuthoring : MonoBehaviour
{
    public Color color;
    public float3 position;

    public float4 ColorAsFloat4 {
        get {
            return new float4(color.r, color.g, color.b, color.a);
        }
    }
    private class Baker : Baker<VoxelAuthoring> {
        public override void Bake(VoxelAuthoring authoring) {
            Entity entity = GetEntity(TransformUsageFlags.Renderable);
            AddComponent(entity, new Voxel { color = authoring.ColorAsFloat4, position = authoring.position });
            AddComponent(entity, new URPMaterialPropertyBaseColor { Value = authoring.ColorAsFloat4 });
        }
    }

}


public struct Voxel : IComponentData{
    public float3 position;
    public float4 color;    
}
