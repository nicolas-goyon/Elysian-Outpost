using Unity.Mathematics;

public struct VoxelDataPosition
{
    public int3 position;
    public int voxelId;

    public VoxelDataPosition(int3 position, int voxelId) {
        this.position = position;
        this.voxelId = voxelId;
    }

}
