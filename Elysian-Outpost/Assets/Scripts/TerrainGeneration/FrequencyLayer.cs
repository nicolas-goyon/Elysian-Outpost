using UnityEngine;

namespace TerrainGeneration
{
    public class FrequencyLayer
    {
        private readonly float _frequency;     // fréquence du bruit (ex: 0.01)
        private readonly float _amplitude;     // poids de cette couche dans la heightmap
        private readonly bool _useTerrace;     // appliquer un “pallier” sur cette couche ?
        private readonly int _terraceSteps;    // nombre de marches pour le pallier de cette couche
        private readonly PerlinNoise _noise;
        private bool _useRoundPosition;
        private int _roundPositionDigits; 

        public FrequencyLayer(float frequency, float amplitude, PerlinNoise noise ,bool useTerrace = false, int terraceSteps = 1, bool useRoundPosition = false, int roundPositionDigits = 2)
        {
            this._frequency   = frequency;
            this._amplitude   = amplitude;
            this._useTerrace  = useTerrace;
            this._terraceSteps = Mathf.Max(terraceSteps, 1);
            this._noise       = noise;
            this._useRoundPosition = useRoundPosition;
            this._roundPositionDigits = roundPositionDigits;
        }
        
        /**
         * Applique un “pallier” simple sur une valeur [0,1] en N marches
         */
        public static float ApplyTerrace01(float v, int steps)
        {
            v = Mathf.Clamp01(v);
            if (steps <= 1) return v;

            float level = Mathf.Round(v * (steps - 1));
            return level / (steps - 1);
        }

        // Calcule la hauteur normalisée [0,1] à partir de toutes les couches
        public (float ampNoised, float baseAmplitude) GetHeight01At(int worldX, int worldZ)
        {
            
            float x = worldX;
            float z = worldZ;
            
            if (_useRoundPosition)
            {
                x = Mathf.Round(x / _roundPositionDigits) * _roundPositionDigits;
                z = Mathf.Round(z / _roundPositionDigits) * _roundPositionDigits;
            }
            
            float n = _noise.Noise(x * _frequency, z * _frequency); // [0,1]

            if (_useTerrace && _terraceSteps > 1)
                n = ApplyTerrace01(n, _terraceSteps);

            return (n* _amplitude, _amplitude);
        }
    }

}