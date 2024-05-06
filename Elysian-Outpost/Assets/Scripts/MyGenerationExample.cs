using System;
using UnityEngine;
using VoxelMaster;

public class MyGenerationExample : BaseGeneration
{
    [SerializeField] private GenerationSettings generationSettings;

    [SerializeField] private short AirBlock = -1;

    public override short Generation(int x, int y, int z) // The generation action only takes coordinates, you must return a block ID. 
    {
        return NoiseLevelTest(x, y, z);
    }

    private short NoiseLevelTest(int x, int y, int z) {
        if (generationSettings.isFlat) {
            return flatGeneration(x, y, z);
        }
        

        short level = GetLevel(x, y, z);
        // if Y is above the level (with level height), return air
        if (y > level * generationSettings.stepsHeight) {
            return AirBlock;
        }

        return level;

    }

    private short flatGeneration(int x, int y, int z) {
        if (y != 10) {
            return AirBlock;
        }

        return GetLevel(x, y, z);
    }

    // Returns the level of the step within 6 steps
    private short GetLevel(int x, int y, int z) {
        
        float maxValue = generationSettings.maxBlockID;

        float numberOfLevels = generationSettings.numberOfSteps;

        // Create a perlin noise with a min and max value
        double perlinXDouble = x / generationSettings.stepsPerlinNoiseScale;
        double perlinYDouble = z / generationSettings.stepsPerlinNoiseScale;

        int round = 1;
        perlinXDouble = Math.Round(perlinXDouble, round);
        perlinYDouble = Math.Round(perlinYDouble, round);

        float perlinX = (float)perlinXDouble;
        float perlinY = (float)perlinYDouble;

        float perlinNoise = Mathf.PerlinNoise(perlinX, perlinY);
        float value = perlinNoise * numberOfLevels;

        float finalValue = value + 1;

        if (finalValue > maxValue) {
            Debug.Log("Value is greater than max value: " + finalValue);
            return AirBlock;
        }
        if (finalValue < 1) {
            Debug.Log("Value is lower than 1: " + finalValue);
            return 1;
        }


        return (short)finalValue;
    }

    //private short allColorByLevel(int x, int y, int z) {
    //    float plainHeight = 7f; // Difference of height between each level
    //    int numberOfLevels = 12;
    //    int blockIDMinLevel = 1;
    //    float plainMaxHeight = plainHeight * numberOfLevels;


    //    // if the Y is lower that 30, then sand over stone

    //    if (y < 0 || y > plainMaxHeight) {
    //        return -1;
    //    }

    //    return (short)(blockIDMinLevel + (y / plainHeight));
    //}
}