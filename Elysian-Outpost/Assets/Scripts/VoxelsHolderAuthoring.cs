using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class VoxelsHolderAuthoring : MonoBehaviour
{
    public VoxelsHolderSO voxelsHolderSO;

    public class Baker : Baker<VoxelsHolderAuthoring> {
        public override void Bake(VoxelsHolderAuthoring authoring) {
            BlobBuilder blobBuilder = new(Allocator.Temp);

            ref BlobVoxelsHolder blobVoxelsHolder = ref blobBuilder.ConstructRoot<BlobVoxelsHolder>();
            BlobBuilderArray<VoxelType> voxelsData = blobBuilder.Allocate(ref blobVoxelsHolder.voxels, authoring.voxelsHolderSO.voxels.Length);

            for (int i = 0; i < authoring.voxelsHolderSO.voxels.Length; i++) {
                VoxelTypeSO voxelSO = authoring.voxelsHolderSO.voxels[i];
                voxelsData[i] = new VoxelType(new float4(voxelSO.color.r, voxelSO.color.g, voxelSO.color.b, voxelSO.color.a));
            }

            BlobAssetReference<BlobVoxelsHolder> blobAssetReference = blobBuilder.CreateBlobAssetReference<BlobVoxelsHolder>(Allocator.Persistent);

            blobBuilder.Dispose();

            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new VoxelsHolder { BlobAssetReference = blobAssetReference });

        }

    }
}

public struct VoxelsHolder : IComponentData, IDisposable { 
    public BlobAssetReference<BlobVoxelsHolder> BlobAssetReference;

    public void Dispose() {
        BlobAssetReference.Dispose();
    }
}

public struct BlobVoxelsHolder : IComponentData { 
    public BlobArray<VoxelType> voxels;
}
