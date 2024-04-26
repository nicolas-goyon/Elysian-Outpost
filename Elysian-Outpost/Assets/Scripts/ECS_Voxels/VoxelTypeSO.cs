using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "VoxelType", menuName = "Voxels/New Voxel")]
public class VoxelTypeSO : ScriptableObject
{
    public string voxelName;
    public int voxelId;
    public Color color;


    public float4 GetFloatColor() {
        return new float4(color.r, color.g, color.b, color.a);
    }
}
