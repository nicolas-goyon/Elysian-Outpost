using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class VoxelsHolderAuthoring : MonoBehaviour
{
    public VoxelsHolderSO voxelsHolderSO;

    public class Baker : Baker<VoxelsHolderAuthoring> {
        public override void Bake(VoxelsHolderAuthoring authoring) {
            Entity entity = GetEntity(TransformUsageFlags.None);
            BlobBuilder blobBuilder = new(Allocator.Temp);

            ref BlobVoxelsHolder blobVoxelsHolder = ref blobBuilder.ConstructRoot<BlobVoxelsHolder>();
            BlobBuilderArray<VoxelType> voxelsData = blobBuilder.Allocate(ref blobVoxelsHolder.voxels, authoring.voxelsHolderSO.voxels.Length);

            for (int i = 0; i < authoring.voxelsHolderSO.voxels.Length; i++) {
                VoxelTypeSO voxelSO = authoring.voxelsHolderSO.voxels[i];
                voxelsData[i] = new VoxelType(new float4(voxelSO.color.r, voxelSO.color.g, voxelSO.color.b, voxelSO.color.a));
            }

            BlobAssetReference<BlobVoxelsHolder> blobAssetReference = blobBuilder.CreateBlobAssetReference<BlobVoxelsHolder>(Allocator.Persistent);

            AddComponent(entity, new VoxelsHolder { BlobAssetReference = blobAssetReference });

            blobBuilder.Dispose();
        }
    }
}

public struct VoxelsHolder : IComponentData {
    public BlobAssetReference<BlobVoxelsHolder> BlobAssetReference;
}

public struct BlobVoxelsHolder : IComponentData { 
    public BlobArray<VoxelType> voxels;
}

public struct VoxelType {
    public float4 floatColor;

    public VoxelType(float4 color) {
        this.floatColor = color;
    }


}
