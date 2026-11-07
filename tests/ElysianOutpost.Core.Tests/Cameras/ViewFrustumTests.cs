// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Cameras;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.Core.Tests.Cameras;

public sealed class ViewFrustumTests
{
    private static readonly FreeFlyCameraSettings _settings = new();

    // The camera at the origin looks along +Z (yaw 0, pitch 0). The lens of the app: 60 degrees, near 0.3, far 1000.
    private static ViewFrustum MakeFrustum(float3 position, float yaw = 0f, float pitch = 0f, float aspectRatio = 16f / 9f)
    {
        var camera = new FreeFlyCamera(_settings, position, yaw, pitch);
        return ViewFrustum.FromViewProjection(camera.CreateViewMatrix() * new CameraLens().CreateProjectionMatrix(aspectRatio));
    }

    [Theory]
    [InlineData(-1f, -1f, 10f, true)] // In front of the camera.
    [InlineData(-1f, -1f, -12f, false)] // Behind the camera.
    [InlineData(-1f, -1f, 1001f, false)] // After the far plane.
    [InlineData(-1f, -1f, 0.1f, true)] // On the near plane: the box goes from 0.1 to 2.1.
    [InlineData(40f, -1f, 10f, false)] // To the right, outside the field of view.
    [InlineData(-42f, -1f, 10f, false)] // To the left, outside the field of view.
    [InlineData(-1f, 30f, 10f, false)] // Above the view.
    [InlineData(-1f, -32f, 10f, false)] // Below the view.
    public void Intersects_BoxOfTwoVoxels_GivesTheExpectedResult(float x, float y, float z, bool expected)
    {
        ViewFrustum frustum = MakeFrustum(float3.Zero);
        var min = new float3(x, y, z);

        Assert.Equal(expected, frustum.Intersects(min, min + new float3(2f)));
    }

    [Fact]
    public void Intersects_LargeBoxAroundTheCamera_IsTrue()
    {
        ViewFrustum frustum = MakeFrustum(new float3(5f, 5f, 5f), yaw: 123f, pitch: 40f);

        Assert.True(frustum.Intersects(float3.Zero, new float3(10f)));
    }

    // The acceptance test of #50: the camera at a corner of the world, pointed outside the world, sees no chunk.
    [Theory]
    [InlineData(-256f, 47.1f, -256f, 225f)] // The corner (-X, -Z): the camera looks to -X and -Z.
    [InlineData(256f, 47.1f, 256f, 45f)] // The corner (+X, +Z): the camera looks to +X and +Z.
    public void Intersects_CameraAtAWorldCornerPointedOutside_SeesNoChunk(float x, float y, float z, float yaw)
    {
        ViewFrustum frustum = MakeFrustum(new float3(x, y, z), yaw);
        WorldSettings world = WorldSettings.SampleScene;
        int3 size = world.ChunkSize;

        int visible = world.GetChunkPositions(default).Count(chunk => frustum.Intersects(ToFloat3(chunk), ToFloat3(chunk + size)));

        Assert.Equal(0, visible);
    }

    // The start camera of the app (0, 47.1, -10) looks along +Z. Most of the chunks behind it are not in the view.
    [Fact]
    public void Intersects_StartCamera_SeesLessThanHalfOfTheChunks()
    {
        ViewFrustum frustum = MakeFrustum(new float3(0f, 47.1f, -10f));
        WorldSettings world = WorldSettings.SampleScene;
        int3[] chunks = world.GetChunkPositions(default);

        int visible = chunks.Count(chunk => frustum.Intersects(ToFloat3(chunk), ToFloat3(chunk + world.ChunkSize)));

        Assert.InRange(visible, 1, (chunks.Length / 2) - 1);
    }

    // The test is conservative: if a point of the box is in the view, the box intersects the view.
    // Random cameras and boxes, against points of each box that the projection puts in the clip volume.
    [Fact]
    public void Intersects_BoxWithAPointInTheView_IsAlwaysTrue()
    {
        var random = new Random(50);
        int boxesInView = 0;
        for (int i = 0; i < 2000; i++)
        {
            var camera = new FreeFlyCamera(_settings, RandomPoint(random, 20f), (float)(random.NextDouble() * 360), (float)((random.NextDouble() * 170) - 85));
            float4x4 viewProjection = camera.CreateViewMatrix() * new CameraLens().CreateProjectionMatrix(1.5f);
            var frustum = ViewFrustum.FromViewProjection(viewProjection);
            float3 min = RandomPoint(random, 30f);
            float3 max = min + new float3((float)random.NextDouble() * 10, (float)random.NextDouble() * 10, (float)random.NextDouble() * 10);

            bool anyPointInView = false;
            for (int sample = 0; sample < 64 && !anyPointInView; sample++)
            {
                var point = new float3(
                    float.Lerp(min.X, max.X, (float)random.NextDouble()),
                    float.Lerp(min.Y, max.Y, (float)random.NextDouble()),
                    float.Lerp(min.Z, max.Z, (float)random.NextDouble()));
                float4 clip = FreeFlyCameraTests.TransformRow(point, viewProjection);
                anyPointInView = clip.W > 0 && Math.Abs(clip.X) <= clip.W && Math.Abs(clip.Y) <= clip.W && clip.Z >= 0 && clip.Z <= clip.W;
            }

            if (anyPointInView)
            {
                boxesInView++;
                Assert.True(frustum.Intersects(min, max), $"Box {i} has a point in the view, but the test says that it is outside.");
            }
        }

        Assert.True(boxesInView > 100, $"Only {boxesInView} boxes were in the view.");
    }

    private static float3 ToFloat3(int3 value) => new(value.X, value.Y, value.Z);

    private static float3 RandomPoint(Random random, float range) => new(
        (float)((random.NextDouble() * 2) - 1) * range,
        (float)((random.NextDouble() * 2) - 1) * range,
        (float)((random.NextDouble() * 2) - 1) * range);
}
