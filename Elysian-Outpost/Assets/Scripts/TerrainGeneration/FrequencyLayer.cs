using UnityEngine;

namespace TerrainGeneration
{
    public class FrequencyLayer
    {
        private readonly float _frequency;     // fréquence du bruit (ex: 0.01)
        private readonly float _amplitude;     // poids de cette couche dans la heightmap
        private readonly int _terraceSteps;    // nombre de marches pour le pallier de cette couche
        private readonly PerlinNoise _noise;
        private readonly int _roundPositionDigits; 

        /**
         * FréquenceLayer représente une couche de bruit avec une fréquence, une amplitude, et des options de pallier et d'arrondi.
         * @param frequency Fréquence du bruit
         * @param amplitude Amplitude (poids) de cette couche
         * @param noise Instance de PerlinNoise à utiliser
         * @param terraceSteps Nombre de marches pour le pallier (1 = pas de pallier)
         * @param roundPositionDigits Nombre de chiffres pour arrondir les positions (1 = pas d'arrondi) (créer des motifs carrés)
         */
        public FrequencyLayer(float frequency, float amplitude, PerlinNoise noise ,int terraceSteps = 1, int roundPositionDigits = 1)
        {
            this._frequency   = frequency;
            this._amplitude   = amplitude;
            this._terraceSteps = Mathf.Max(terraceSteps, 1);
            this._noise       = noise;
            this._roundPositionDigits = roundPositionDigits;
        }
        
        /**
         * Applique un “pallier” simple sur une valeur [0,1] en N marches
         */
        private static float ApplyTerrace01(float v, int steps)
        {
            v = Mathf.Clamp01(v);
            if (steps <= 1) return v;

            float level = Mathf.Round(v * (steps - 1));
            return level / (steps - 1);
        }

        // Calcule la hauteur normalisée [0,1] à partir de toutes les couches
        public (float ampNoised, float baseAmplitude) GetHeight01At(int worldX, int worldZ)
        {
            
            float x = Mathf.Round(((float)worldX) / _roundPositionDigits) * _roundPositionDigits;
            float z = Mathf.Round(((float)worldZ) / _roundPositionDigits) * _roundPositionDigits;
            
            float n = _noise.Noise(x * _frequency, z * _frequency); // [0,1]

            n = _terraceSteps > 1 ? ApplyTerrace01(n, _terraceSteps) : n;

            return (n* _amplitude, _amplitude);
        }
    }

}