// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.WorldGeneration;

/// <summary>
/// One layer of noise of the heightmap: a frequency, an amplitude, terrace steps and a rounding of the positions.
/// <see cref="MainGeneration"/> adds the layers to get the terrain height.
/// </summary>
public class FrequencyLayer
{
    // The frequency of the noise, for example 0.01.
    private readonly float _frequency;

    // The weight of this layer in the heightmap.
    private readonly float _amplitude;

    // The number of terrace steps of this layer. 0 or 1: no steps.
    private readonly float _terraceSteps;
    private readonly PerlinNoise _noise;

    // The size of the squares of the rounded positions, in voxels. 1: no rounding.
    private readonly int _roundPositionDigits;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrequencyLayer"/> class.
    /// </summary>
    /// <param name="frequency">The frequency of the noise. A low frequency gives large hills.</param>
    /// <param name="amplitude">The weight of this layer in the heightmap.</param>
    /// <param name="noise">The noise of the layer.</param>
    /// <param name="terraceSteps">The number of terrace steps. 0 or 1 gives no steps.</param>
    /// <param name="roundPositionDigits">
    /// The size of the squares of the rounded positions, in voxels. The layer rounds each position to a multiple of this value,
    /// thus the terrain has square areas of the same height. 1 gives no rounding. The name comes from the legacy code.
    /// </param>
    public FrequencyLayer(float frequency, float amplitude, PerlinNoise noise, float terraceSteps = 0, int roundPositionDigits = 1)
    {
        this._frequency = frequency;
        this._amplitude = amplitude;
        this._terraceSteps = terraceSteps;
        this._noise = noise;
        this._roundPositionDigits = roundPositionDigits;
    }

    // Rounds a value from 0 to 1 to one of the steps values 0, 1 / (steps − 1), ..., 1.
    private static float ApplyTerrace01(float v, float steps)
    {
        v = Math.Clamp(v, 0f, 1f);
        if (steps <= 1) return v;

        float level = MathF.Round(v * (steps - 1));
        return level / (steps - 1);
    }

    /// <summary>
    /// Gets the height of this layer at a world position.
    /// </summary>
    /// <param name="worldX">The X coordinate of the world position.</param>
    /// <param name="worldZ">The Z coordinate of the world position.</param>
    /// <returns>
    /// The noise value from 0 to 1 multiplied by the amplitude, and the amplitude. The caller divides the sum of the first values
    /// of all the layers by the sum of the amplitudes.
    /// </returns>
    public (float ampNoised, float baseAmplitude) GetHeight01At(int worldX, int worldZ)
    {

        float x = MathF.Round(((float)worldX) / _roundPositionDigits) * _roundPositionDigits;
        float z = MathF.Round(((float)worldZ) / _roundPositionDigits) * _roundPositionDigits;

        // The noise gives a value from 0 to 1.
        float n = _noise.Noise(x * _frequency, z * _frequency);

        n = _terraceSteps > 0 ? ApplyTerrace01(n, _terraceSteps) : n;

        return (n * _amplitude, _amplitude);
    }
}
