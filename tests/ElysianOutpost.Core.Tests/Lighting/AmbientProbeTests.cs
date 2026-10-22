// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Lighting;

namespace ElysianOutpost.Core.Tests.Lighting;

public sealed class AmbientProbeTests
{
    private const float Tolerance = 2e-4f;

    // The ambient values of the default procedural skybox for the six faces of a voxel.
    // The calculation outside the app evaluated its spherical harmonics in these directions. See AmbientProbe.LegacyDefaultSkybox.
    // The sky above is blue, and the faces that point to the sun (+X and -Z) get more light.
    [Theory]
    [InlineData(1f, 0f, 0f, 0.1958f, 0.2462f, 0.3399f)]
    [InlineData(-1f, 0f, 0f, 0.1761f, 0.2192f, 0.2972f)]
    [InlineData(0f, 1f, 0f, 0.1478f, 0.2229f, 0.3791f)]
    [InlineData(0f, -1f, 0f, 0.1469f, 0.1308f, 0.1172f)]
    [InlineData(0f, 0f, 1f, 0.1762f, 0.2190f, 0.2952f)]
    [InlineData(0f, 0f, -1f, 0.2104f, 0.2658f, 0.3691f)]
    public void LegacyDefaultSkybox_FaceDirections_GiveTheValuesOfTheSkyboxCalculation(float x, float y, float z, float red, float green, float blue)
    {
        AssertClose(new float3(red, green, blue), AmbientProbe.LegacyDefaultSkybox.Evaluate(new float3(x, y, z)));
    }

    [Fact]
    public void LegacyDefaultSkybox_DiagonalDirection_GivesTheValueOfTheSkyboxCalculation()
    {
        float3 normal = float3.Normalize(new float3(1f, 1f, 1f));

        AssertClose(new float3(0.16511f, 0.22964f, 0.35383f), AmbientProbe.LegacyDefaultSkybox.Evaluate(normal));
    }

    // A probe with only one coefficient gives the term of that coefficient: k0 + k1 y + k2 z + k3 x + k4 x y + k5 y z
    // + k6 (3 z² - 1) + k7 x z + k8 (x² - y²). The normals are not unit vectors here, to examine each product.
    [Theory]
    [InlineData(0, 0.6f, 0.7f, 0.8f, 1f)]
    [InlineData(1, 0.6f, 0.7f, 0.8f, 0.7f)]
    [InlineData(2, 0.6f, 0.7f, 0.8f, 0.8f)]
    [InlineData(3, 0.6f, 0.7f, 0.8f, 0.6f)]
    [InlineData(4, 0.6f, 0.7f, 0.8f, 0.42f)]
    [InlineData(5, 0.6f, 0.7f, 0.8f, 0.56f)]
    [InlineData(6, 0.6f, 0.7f, 0.8f, 0.92f)]
    [InlineData(7, 0.6f, 0.7f, 0.8f, 0.48f)]
    [InlineData(8, 0.8f, 0.6f, 0f, 0.28f)]
    public void Evaluate_OneCoefficient_GivesTheTermOfThatCoefficient(int index, float x, float y, float z, float expected)
    {
        var coefficients = new float3[AmbientProbe.CoefficientCount];
        coefficients[index] = new float3(1f, 2f, 3f);
        var probe = new AmbientProbe(coefficients);

        AssertClose(new float3(expected, 2f * expected, 3f * expected), probe.Evaluate(new float3(x, y, z)), 1e-5f);
    }

    // A negative value is not a light. For the horizontal normal (1, 0, 0), the term 3 z² - 1 is -1.
    [Fact]
    public void Evaluate_NegativeValue_GivesZero()
    {
        var coefficients = new float3[AmbientProbe.CoefficientCount];
        coefficients[0] = new float3(0.2f, 0.5f, 1f);
        coefficients[6] = new float3(0.4f, 0.4f, 0.4f);
        var probe = new AmbientProbe(coefficients);

        AssertClose(new float3(0f, 0.1f, 0.6f), probe.Evaluate(new float3(1f, 0f, 0f)), 1e-6f);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(10)]
    public void Constructor_NotNineCoefficients_ThrowsArgumentException(int count)
    {
        Assert.Throws<ArgumentException>(() => new AmbientProbe(new float3[count]));
    }

    [Fact]
    public void Coefficients_AreACopyOfTheConstructorArgument()
    {
        var coefficients = new float3[AmbientProbe.CoefficientCount];
        coefficients[0] = new float3(1f, 1f, 1f);
        var probe = new AmbientProbe(coefficients);

        coefficients[0] = new float3(5f, 5f, 5f);

        Assert.Equal(new float3(1f, 1f, 1f), probe.Coefficients[0]);
        Assert.Equal(AmbientProbe.CoefficientCount, probe.Coefficients.Length);
    }

    private static void AssertClose(float3 expected, float3 actual, float tolerance = Tolerance)
    {
        Assert.True(
            MathF.Abs(expected.X - actual.X) <= tolerance
            && MathF.Abs(expected.Y - actual.Y) <= tolerance
            && MathF.Abs(expected.Z - actual.Z) <= tolerance,
            $"Expected {expected}, actual {actual}.");
    }
}
