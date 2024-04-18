using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

public class VoxelControl : MonoBehaviour
{
    public static VoxelControl Instance { get; private set; }

    private void Awake() {
        if (Instance != null && Instance != this) {
            throw new System.Exception("Multiple instances of VoxelControl found");
        } else {
            Instance = this;
        }
    }

    private VoxelData[][] voxelDatas;


    private readonly int width = 16;
    private readonly int height = 16;

    private int index = 0;

    private bool isSpaceUp = true;
    Unity.Mathematics.Random random = new Unity.Mathematics.Random(123);


    private void Start() {
        voxelDatas = new VoxelData[width][];
        for (int i = 0; i < width; i++) {
            voxelDatas[i] = new VoxelData[height];
        }
    }





    [BurstCompile]
    private void Update() {
        if (isSpaceUp && Input.GetKey(KeyCode.Space)) {
            isSpaceUp = false;
            CreateVoxel();
        } else {
            isSpaceUp = true;
        }   
    }
    private void CreateVoxel() {
        // Create a new VoxelData at the current index
        int4 randomColor = new((byte) random.NextInt(0, 255), (byte) random.NextInt(0, 255), (byte) random.NextInt(0, 255), 255);
        voxelDatas[index % width][index / width] = new VoxelData(1, randomColor);
        index++;
        
    }

    public VoxelData[][] GetVoxelDatas() {
        return voxelDatas;
    }

    public int GetNumberOfVoxels() {
        return index;
    }


}
