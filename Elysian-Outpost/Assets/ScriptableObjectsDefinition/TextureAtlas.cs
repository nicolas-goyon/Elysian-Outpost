using System;
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
                Debug.Log("Atlas sizes are valid.");
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

            float u0 = (x + 0.5f) / AtlasSizeX;
            float v0 = (y + 0.5f) / AtlasSizeY;
            float u1 = (x + w - 0.5f) / AtlasSizeX;
            float v1 = (y + h - 0.5f) / AtlasSizeY;
            
            // Round down to 0.01 precision to avoid floating point issues
            // FIXME: This is a hacky workaround, find a better solution
            u0 = (float)Math.Round(u0, 2);
            v0 = (float)Math.Round(v0, 2);
            u1 = (float)Math.Round(u1, 2);
            v1 = (float)Math.Round(v1, 2);
            
            

            return (u0, v0, u1, v1);
        }
    }
}
