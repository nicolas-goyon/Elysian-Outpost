// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Lighting;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

namespace ElysianOutpost.Core.Tests.Lighting;

public sealed class SceneLightingTests
{
    private const float Tolerance = 1e-5f;

    [Theory]
    [InlineData(0f, 0f, 0f, 0f, 1f)]
    [InlineData(90f, 0f, 0f, -1f, 0f)]
    [InlineData(-90f, 0f, 0f, 1f, 0f)]
    [InlineData(0f, 90f, 1f, 0f, 0f)]
    [InlineData(0f, -90f, -1f, 0f, 0f)]
    [InlineData(0f, 180f, 0f, 0f, -1f)]
    public void UnityForward_AxisAngles_GivesTheUnityForwardDirection(float eulerX, float eulerY, float x, float y, float z)
    {
        AssertClose(new float3(x, y, z), SceneLighting.UnityForward(eulerX, eulerY));
    }

    // The scene keeps the rotation of the light as a quaternion. Its Euler angles in the editor are (50, -30, 0).
    // The rotation of the Z axis by this quaternion must give the same direction.
    [Fact]
    public void UnityForward_LegacyLightAngles_AgreesWithTheQuaternionOfTheScene()
    {
        var rotation = new Quaternion(0.40821788f, -0.23456968f, 0.10938163f, 0.8754261f);
        Vector3 rotated = Vector3.Transform(Vector3.UnitZ, rotation);

        float3 forward = SceneLighting.UnityForward(50f, -30f);

        AssertClose(new float3(rotated.X, rotated.Y, rotated.Z), forward);
    }

    // The scene keeps the rotation of its directional light as a quaternion. The light goes along the rotated Z axis.
    [Fact]
    public void LegacySampleScene_DirectionToLight_IsTheOppositeOfTheRotatedZAxisOfTheLight()
    {
        var rotation = new Quaternion(0.40821788f, -0.23456968f, 0.10938163f, 0.8754261f);
        Vector3 lightDirection = Vector3.Transform(Vector3.UnitZ, rotation);

        float3 directionToLight = SceneLighting.LegacySampleScene.DirectionToLight;

        AssertClose(new float3(-lightDirection.X, -lightDirection.Y, -lightDirection.Z), directionToLight);
    }

    // The light of the scene has the intensity 2 and a color temperature of 5000 K. The test calculates the color of 5000 K:
    // the chromaticity of a black body (Kim et al. approximation of the Planckian locus), then the linear sRGB values,
    // with the largest value equal to 1.
    [Fact]
    public void LegacySampleScene_LightColor_IsTheIntensityMultipliedByTheColorOf5000K()
    {
        const double temperature = 5000;
        double x = (-3.0258469e9 / Math.Pow(temperature, 3)) + (2.1070379e6 / Math.Pow(temperature, 2)) + (0.2226347e3 / temperature) + 0.240390;
        double y = (3.0817580 * x * x * x) - (5.87338670 * x * x) + (3.75112997 * x) - 0.37001483;
        double bigX = x / y;
        double bigZ = (1 - x - y) / y;
        double red = (3.2404542 * bigX) - 1.5371385 - (0.4985314 * bigZ);
        double green = (-0.9692660 * bigX) + 1.8760108 + (0.0415560 * bigZ);
        double blue = (0.0556434 * bigX) - 0.2040259 + (1.0572252 * bigZ);
        double largest = Math.Max(red, Math.Max(green, blue));
        var expected = new float3((float)(red / largest), (float)(green / largest), (float)(blue / largest)) * 2f;

        AssertClose(expected, SceneLighting.LegacySampleScene.LightColor, 1e-3f);
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
