using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GenerationSettings", menuName = "Voxel Generation/GenerationSettings")]
public class GenerationSettings : ScriptableObject
{
    [Header("General Settings")]
    public int maxY = 100;
    public int maxBlockID = 12;
    public bool isFlat = false;


    [Header("Steps Settings")]
    public int numberOfSteps = 10;
    public int stepsHeight = 6;
    public float stepsPerlinNoiseScale = 200f;
    public int stepsRound = 1;

}
