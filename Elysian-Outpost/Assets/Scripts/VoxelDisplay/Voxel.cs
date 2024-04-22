using Unity.Entities;
using Unity.Mathematics;


public struct VoxelInfo : IComponentData {
    public float3 position;
    public float4 color;
}
