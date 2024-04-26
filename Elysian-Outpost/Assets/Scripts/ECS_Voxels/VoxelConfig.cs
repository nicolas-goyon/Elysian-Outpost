using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class VoxelConfig : MonoBehaviour
{
    
    [SerializeField] private GameObject voxelPrefab;


    public class Baker : Baker<VoxelConfig> {
        public override void Bake(VoxelConfig authoring) {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new VoxelConfigData {
                voxelPrefab = GetEntity(authoring.voxelPrefab, TransformUsageFlags.Dynamic)
            });
        }
    }

}


public struct VoxelConfigData : IComponentData {
    public Entity voxelPrefab;
}
