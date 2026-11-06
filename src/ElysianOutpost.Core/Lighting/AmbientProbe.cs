// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;

namespace ElysianOutpost.Core.Lighting;

/// <summary>
/// An ambient light that changes with the direction of the normal: L2 spherical harmonics with nine coefficients for each color channel.
/// </summary>
/// <remarks>
/// <para>
/// The coefficients are in the evaluation form. For a unit normal (x, y, z), the ambient color is:
/// k0 + k1 y + k2 z + k3 x + k4 x y + k5 y z + k6 (3 z² - 1) + k7 x z + k8 (x² - y²).
/// The constants of the basis functions and the convolution with the cosine lobe are already in the coefficients.
/// Thus the value is the irradiance divided by π, as <c>SampleSH</c> of Unity gives it.
/// </para>
/// <para>
/// The fragment shader of the chunks (<c>chunk.frag</c>) uses the same formula.
/// A negative value is not a light, thus <see cref="Evaluate"/> and the shader change it to zero, as Unity does.
/// </para>
/// </remarks>
public sealed class AmbientProbe
{
    /// <summary>
    /// The number of coefficients for each color channel.
    /// </summary>
    public const int CoefficientCount = 9;

    private readonly float3[] _coefficients;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmbientProbe"/> class.
    /// </summary>
    /// <param name="coefficients">The nine coefficients k0 to k8, in the evaluation form. Each coefficient has the red, green and blue values.</param>
    /// <exception cref="ArgumentException">The number of coefficients is not nine.</exception>
    public AmbientProbe(ReadOnlySpan<float3> coefficients)
    {
        if (coefficients.Length != CoefficientCount)
        {
            throw new ArgumentException($"An L2 ambient probe has {CoefficientCount} coefficients, not {coefficients.Length}.", nameof(coefficients));
        }

        _coefficients = coefficients.ToArray();
    }

    /// <summary>
    /// Gets an approximation of the ambient light of the legacy scene (<c>SampleScene.unity</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The scene uses the skybox as the source of the ambient light (<c>m_AmbientMode: 0</c>), with the default procedural skybox.
    /// The app has no skybox. Thus these coefficients come from a calculation outside the app.
    /// </para>
    /// <para>
    /// The calculation used a port of the shader <c>Skybox/Procedural</c> of Unity, with the values of the default skybox material
    /// (sun size 0.04, convergence 5, atmosphere thickness 1, sky tint 0.5, ground color (0.369, 0.349, 0.341) and exposure 1.3)
    /// and with the directional light of the scene. It projected the sky on L2 spherical harmonics over 2,000 × 4,000 directions.
    /// A grid of 1,000 × 2,000 directions gave the same coefficients within 0.0003.
    /// </para>
    /// <para>
    /// In Unity, <c>RenderSettings.ambientProbe</c> gives the ambient probe of the scene. Its values can replace these coefficients.
    /// </para>
    /// </remarks>
    public static AmbientProbe LegacyDefaultSkybox { get; } = new(
    [
        new float3(0.17552f, 0.21732f, 0.29963f),
        new float3(0.00048f, 0.04605f, 0.13096f),
        new float3(-0.01711f, -0.02339f, -0.03698f),
        new float3(0.00988f, 0.01350f, 0.02135f),
        new float3(0.00930f, 0.01195f, 0.01862f),
        new float3(-0.01610f, -0.02069f, -0.03225f),
        new float3(0.00888f, 0.01256f, 0.01626f),
        new float3(-0.01274f, -0.01692f, -0.02354f),
        new float3(0.01930f, 0.02791f, 0.03519f),
    ]);

    /// <summary>
    /// Gets the nine coefficients k0 to k8, in the evaluation form.
    /// </summary>
    public ReadOnlySpan<float3> Coefficients => _coefficients;

    /// <summary>
    /// Gives the ambient color for a normal.
    /// </summary>
    /// <param name="normal">The unit normal, in world axes.</param>
    /// <returns>The ambient color, with linear values of zero or more.</returns>
    public float3 Evaluate(float3 normal)
    {
        float x = normal.X;
        float y = normal.Y;
        float z = normal.Z;
        float3 value = _coefficients[0]
            + (_coefficients[1] * y)
            + (_coefficients[2] * z)
            + (_coefficients[3] * x)
            + (_coefficients[4] * (x * y))
            + (_coefficients[5] * (y * z))
            + (_coefficients[6] * ((3f * z * z) - 1f))
            + (_coefficients[7] * (x * z))
            + (_coefficients[8] * ((x * x) - (y * y)));
        return new float3(MathF.Max(value.X, 0f), MathF.Max(value.Y, 0f), MathF.Max(value.Z, 0f));
    }
}
