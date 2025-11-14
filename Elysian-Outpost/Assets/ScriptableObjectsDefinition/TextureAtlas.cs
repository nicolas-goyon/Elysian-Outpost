using System;
using Base.InGameConsole;
using UnityEngine;

namespace ScriptableObjectsDefinition
{
    [CreateAssetMenu(fileName = "TextureAtlas", menuName = "Scriptable Objects/TextureAtlas")]
    public class TextureAtlas : ScriptableObject
    {
        public Texture2D AtlasTexture;
        public int TileSizeX = 16; // Size of each tile in pixels
        public int TileSizeY = 16; // Size of each tile in pixels
        
        public string MaterialName = "voxel_mat";
        public string TextureFilePath = "TextureAtlasRessource.png";
        
        private const float textureOffset = 0.5f; // Offset to avoid texture bleeding

        private int AtlasSizeX => AtlasTexture != null ? AtlasTexture.width : 0;
        private int AtlasSizeY => AtlasTexture != null ? AtlasTexture.height : 0;

        private bool _wasWarned = false;

        private (uint x, uint y, uint width, uint height) GetTextureRect(uint tileIndex)
        {
            int tilesPerRow = AtlasSizeX / TileSizeX;
            int xIndex = (int)(tileIndex % (uint)tilesPerRow);
            int yIndex = (int)(tileIndex / (uint)tilesPerRow);

            uint x = (uint)(xIndex * TileSizeX);
            uint y = (uint)(yIndex * TileSizeY);

            return (x, y, (uint)TileSizeX, (uint)TileSizeY);
        }

        private void OnValidate()
        {
            
            if (AtlasSizeX % TileSizeX != 0 || AtlasSizeY % TileSizeY != 0)
            {
                if (_wasWarned) return;
                _wasWarned = true;
                throw new Exception("Atlas size must be a multiple of tile size.");

            }
            else
            {
                if (!_wasWarned) return;
                _wasWarned = false;
                DebuggerConsole.Log("Atlas sizes are valid.");
            }
        }
        
        /// <summary>
        /// Returns normalized UV coordinates for the texture at the given index.
        /// To avoid bleeding between neighbouring tiles when bilinear filtering is
        /// enabled, the coordinates are adjusted by half a texel on each side
        /// following the "half pixel correction" described here:
        /// https://gamedev.stackexchange.com/questions/46963/how-to-avoid-texture-bleeding-in-a-texture-atlas
        /// </summary>
        /// <param name="index">Index of the texture.</param>
        /// <returns>Tuple containing min/max UV coordinates (u0, v0, u1, v1).</returns>
        public (float u0, float v0, float u1, float v1) GetTextureUv(uint index)
        {
            (uint x, uint y, uint w, uint h) = GetTextureRect(index);

            float u0 = (x + textureOffset) / AtlasSizeX;
            float v0 = (y + textureOffset) / AtlasSizeY;
            float u1 = (x + w - textureOffset) / AtlasSizeX;
            float v1 = (y + h - textureOffset) / AtlasSizeY;

            return (u0, v0, u1, v1);
        }
    }
}
