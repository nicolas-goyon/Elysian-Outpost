// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Cameras;

namespace ElysianOutpost.Core.Tests.Cameras;

public sealed class CameraLensTests
{
    private const float Tolerance = 1e-5f;

    private static readonly CameraLens _legacy = new();

    [Fact]
    public void DefaultValues_AreTheValuesOfTheLegacyScene()
    {
        Assert.Equal(60f, _legacy.VerticalFieldOfView);
        Assert.Equal(0.3f, _legacy.NearPlane);
        Assert.Equal(1000f, _legacy.FarPlane);
    }

    [Fact]
    public void Projection_PointOnTheNearPlane_HasTheDepthZero()
    {
        float4x4 projection = _legacy.CreateProjectionMatrix(16f / 9f);

        Assert.Equal(0f, Depth(new float3(0f, 0f, 0.3f), projection), Tolerance);
    }

    [Fact]
    public void Projection_PointOnTheFarPlane_HasTheDepthOne()
    {
        float4x4 projection = _legacy.CreateProjectionMatrix(16f / 9f);

        Assert.Equal(1f, Depth(new float3(0f, 0f, 1000f), projection), Tolerance);
    }

    [Fact]
    public void Projection_DepthIncreasesWithTheDistance()
    {
        float4x4 projection = _legacy.CreateProjectionMatrix(1f);

        float near = Depth(new float3(0f, 0f, 1f), projection);
        float middle = Depth(new float3(0f, 0f, 10f), projection);
        float far = Depth(new float3(0f, 0f, 500f), projection);

        Assert.True(near > 0f && near < middle && middle < far && far < 1f);
    }

    // Vulkan: the Y axis of the clip space points down. Thus the top of the view has Y = -1 after the division by W.
    [Fact]
    public void Projection_TopEdgeOfTheView_HasYMinusOne()
    {
        float4x4 projection = _legacy.CreateProjectionMatrix(16f / 9f);
        float top = 10f * MathF.Tan(float.DegreesToRadians(30f));

        float4 clip = FreeFlyCameraTests.TransformRow(new float3(0f, top, 10f), projection);

        Assert.Equal(-1f, clip.Y / clip.W, Tolerance);
        Assert.Equal(0f, clip.X / clip.W, Tolerance);
    }

    [Fact]
    public void Projection_BottomEdgeOfTheView_HasYPlusOne()
    {
        float4x4 projection = _legacy.CreateProjectionMatrix(16f / 9f);
        float bottom = -20f * MathF.Tan(float.DegreesToRadians(30f));

        float4 clip = FreeFlyCameraTests.TransformRow(new float3(0f, bottom, 20f), projection);

        Assert.Equal(1f, clip.Y / clip.W, Tolerance);
    }

    [Fact]
    public void Projection_RightEdgeOfTheView_HasXPlusOne()
    {
        const float aspect = 16f / 9f;
        float4x4 projection = _legacy.CreateProjectionMatrix(aspect);
        float right = 10f * MathF.Tan(float.DegreesToRadians(30f)) * aspect;

        float4 clip = FreeFlyCameraTests.TransformRow(new float3(right, 0f, 10f), projection);

        Assert.Equal(1f, clip.X / clip.W, Tolerance);
        Assert.Equal(0f, clip.Y / clip.W, Tolerance);
    }

    [Fact]
    public void Projection_WIsTheDistanceAlongTheViewDirection()
    {
        float4x4 projection = _legacy.CreateProjectionMatrix(1.5f);

        float4 clip = FreeFlyCameraTests.TransformRow(new float3(3f, -2f, 42f), projection);

        Assert.Equal(42f, clip.W, Tolerance);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Projection_WrongAspectRatio_Throws(float aspectRatio)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _legacy.CreateProjectionMatrix(aspectRatio));
    }

    [Theory]
    [InlineData(0f, 0.3f, 1000f)]
    [InlineData(180f, 0.3f, 1000f)]
    [InlineData(60f, 0f, 1000f)]
    [InlineData(60f, 10f, 10f)]
    [InlineData(60f, 0.3f, float.PositiveInfinity)]
    [InlineData(float.NaN, 0.3f, 1000f)]
    public void Projection_WrongLens_Throws(float fieldOfView, float nearPlane, float farPlane)
    {
        var lens = new CameraLens { VerticalFieldOfView = fieldOfView, NearPlane = nearPlane, FarPlane = farPlane };

        Assert.Throws<ArgumentOutOfRangeException>(() => lens.CreateProjectionMatrix(1f));
    }

    private static float Depth(float3 viewPoint, float4x4 projection)
    {
        float4 clip = FreeFlyCameraTests.TransformRow(viewPoint, projection);
        return clip.Z / clip.W;
    }
}
