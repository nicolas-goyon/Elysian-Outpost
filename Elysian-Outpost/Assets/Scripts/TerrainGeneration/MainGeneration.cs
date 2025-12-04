using System;
using Unity.Mathematics;
using UnityEngine;
using Random = System.Random;

namespace TerrainGeneration
{
    public class MainGeneration
    {
        private readonly int3 _chunkSize;
        private readonly PerlinNoise _noise;
        private readonly FrequencyLayer[] _frequencyLayers;
        private const uint StepHeight = 8;
        private readonly int _seaLevel;

        public MainGeneration(int3 chunkSize, int seed = 0)
        {
            this._chunkSize = chunkSize;
            float amountOfSteps = ((float)chunkSize.y) / StepHeight;
            _seaLevel = Mathf.RoundToInt(amountOfSteps * 2) - 1;
            PerlinNoise noise1 = new(seed);
            _frequencyLayers = new FrequencyLayer[]{
                new (
                    frequency:.005f, 
                    amplitude:2.0f, 
                    noise:noise1, 
                    terraceSteps:amountOfSteps,
                    roundPositionDigits:10
                ),
            };
        }

        /**
         * Génère un chunk à la position donnée (en coordonnées monde)
         * @param chunkPosition Position du chunk en coordonnées monde (doit être un multiple de chunkSize)
         * @return Tableau 3D de ushort représentant les types de blocs dans le chunk
         */
        public ushort[,,] GenerateChunkAt(int3 chunkPosition)
        {
            int chunkX = chunkPosition.x;
            int chunkY = chunkPosition.y;
            int chunkZ = chunkPosition.z;
        
            ushort[,,] chunk = new ushort[_chunkSize.x, _chunkSize.y, _chunkSize.z];
        
            for (int x = 0; x < _chunkSize.x; x++)
            {
                for (int z = 0; z < _chunkSize.z; z++)
                {
                    for (int y = 0; y < _chunkSize.y; y++)
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
            BEDROCK = 24,
            ERROR = 26
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
    
            int terrainHeight = Mathf.FloorToInt(height01 * _chunkSize.y); // hauteur du terrain (0 à max)
    
            // Niveau de la mer (quelques marches au-dessus du bas)
    
            // ----- 3. Choix du bloc selon la hauteur -----
    
            // Tout ce qui est sous ou au niveau 0 = bedrock
            if (worldY <= 0)
                return (ushort)BlockType.BEDROCK;
            
            if (worldY is 160 or 159 && terrainHeight > 158)
                return (ushort)BlockType.ERROR;
    
            // Au-dessus du terrain
            if (worldY > terrainHeight)
            {
                // Remplir en eau jusqu’au niveau de la mer
                if (worldY <= _seaLevel)
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
                0 when terrainHeight <= _seaLevel + 1 => (ushort)BlockType.SAND,
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

            foreach (FrequencyLayer layer in _frequencyLayers)
            {
                (float ampNoised, float amp) = layer.GetHeight01At(worldX, worldZ);

                sum      += ampNoised;
                totalAmp += amp;
            }

            return totalAmp <= 0f ? 0f : Mathf.Clamp01(sum / totalAmp); // height01 final
        }
    }
}
