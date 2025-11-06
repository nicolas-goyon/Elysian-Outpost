using System;
using Unity.Mathematics;
using UnityEngine;
using Random = System.Random;

namespace TerrainGeneration
{
    public class MainGeneration
    {
        private readonly int3 chunkSize;
        private readonly PerlinNoise noise;
        private readonly FrequencyLayer[] frequencyLayers;

        public MainGeneration(int3 chunkSize, int seed = 0)
        {
            this.chunkSize = chunkSize;
            noise = new PerlinNoise(seed);
            frequencyLayers = new FrequencyLayer[]{
                new FrequencyLayer(frequency:.01f, amplitude:1.0f, noise:noise, useRoundPosition:true, roundPositionDigits:10),
            };
        }

        /// <summary>
        /// Generates a chunk at specific coordinates in the world
        /// </summary>
        /// <param name="chunkX">X coordinate of the chunk in world space</param>
        /// <param name="chunkZ">Z coordinate of the chunk in world space</param>
        /// <returns>A 3D array representing the chunk voxels</returns>
        public ushort[,,] GenerateChunkAt(int3 chunkPosition)
        {
            int chunkX = chunkPosition.x;
            int chunkY = chunkPosition.y;
            int chunkZ = chunkPosition.z;
        
            ushort[,,] chunk = new ushort[chunkSize.x, chunkSize.y, chunkSize.z];
        
            for (int x = 0; x < chunkSize.x; x++)
            {
                for (int z = 0; z < chunkSize.z; z++)
                {
                    for (int y = 0; y < chunkSize.y; y++)
                    {
                        ushort? blockType = GetBlockTypeAt(chunkPosition, new int3(x, y, z));
                        if (blockType.HasValue)
                        {
                            chunk[x, y, z] = blockType.Value;
                        }
                    }
                }
            }

            return chunk;
        }

        private enum BlockType : ushort
        {
            AIR = 0,
            GRASS = 16,
            DIRT = 11,
            STONE = 22,
            SAND = 13,
            WATER = 8,
            BEDROCK = 24
        }
    

        private ushort? GetBlockTypeAt(int3 chunkPosition, int3 localPosition)
        {
            // Coordonnées monde
            int worldX = chunkPosition.x  + localPosition.x;
            int worldY = chunkPosition.y + localPosition.y;
            int worldZ = chunkPosition.z + localPosition.z;
    
            // ----- 1. Bruit de base (heightmap continue) -----
    
            // Taille moyenne d’un plateau ~ 100 cases => fréquence ≈ 1/100
            float height01 = GetHeight01At(worldX, worldZ);   // [0,1]
    
            // ----- 2. Terracing : transformation en niveaux discrets -----
    
            const int levels     = 6;  // nombre de niveaux verticaux (plateaux)
            const int stepHeight = 4;  // hauteur d’un plateau en blocs
            const int baseHeight = 8;  // décalage global vers le haut (par rapport au bedrock)
    
            int level = Mathf.RoundToInt(height01 * (levels - 1)); // 0..levels-1
            int terrainHeight = baseHeight + level * stepHeight;   // hauteur absolue du sol
    
            // Niveau de la mer (quelques marches au-dessus du bas)
            const int seaLevel = baseHeight + stepHeight * 2;
    
            // ----- 3. Choix du bloc selon la hauteur -----
    
            // Tout ce qui est sous ou au niveau 0 = bedrock
            if (worldY <= 0)
                return (ushort)BlockType.BEDROCK;
    
            // Au-dessus du terrain
            if (worldY > terrainHeight)
            {
                // Remplir en eau jusqu’au niveau de la mer
                if (worldY <= seaLevel)
                    return (ushort)BlockType.WATER;
    
                // Sinon air
                return null; // laisser la valeur par défaut (Air) dans le chunk
            }
    
            // On est dans la masse du terrain (≤ terrainHeight)
            int depthFromSurface = terrainHeight - worldY;

            return depthFromSurface switch
            {
                // Bloc de surface
                // Proche de l’eau => sable, sinon herbe
                0 when terrainHeight <= seaLevel + 1 => (ushort)BlockType.SAND,
                0 => (ushort)BlockType.GRASS,
                <= 3 => (ushort)BlockType.DIRT,
                _ => (ushort)BlockType.STONE
            };
        }
        
        // Calcule la hauteur normalisée [0,1] à partir de toutes les couches
        private float GetHeight01At(int worldX, int worldZ)
        {
            float sum = 0f;
            float totalAmp = 0f;

            foreach (FrequencyLayer layer in frequencyLayers)
            {
                (float ampNoised, float amp) = layer.GetHeight01At(worldX, worldZ);

                sum      += ampNoised;
                totalAmp += amp;
            }

            return totalAmp <= 0f ? 0f : Mathf.Clamp01(sum / totalAmp); // height01 final
        }
    }
}
