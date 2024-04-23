using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "VoxelsHolder", menuName = "Voxels/VoxelsHolder", order = 1)]
public class VoxelsHolderSO : ScriptableObject
{
    public VoxelTypeSO[] voxels;
}
