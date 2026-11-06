// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Cameras;

namespace ElysianOutpost.Core.Tests.Cameras;

public sealed class FreeFlyCameraTests
{
    private const float AngleTolerance = 1e-3f;
    private const float PositionTolerance = 1e-3f;

    private static readonly FreeFlyCameraSettings _legacy = new();

    [Fact]
    public void Settings_DefaultValues_AreTheValuesOfTheLegacyScene()
    {
        Assert.Equal(40f, _legacy.Speed);
        Assert.Equal(5f, _legacy.SpeedMultiplier);
        Assert.Equal(0.125f, _legacy.Sensitivity);
        Assert.Equal(-80f, _legacy.MinPitch);
        Assert.Equal(80f, _legacy.MaxPitch);
        Assert.Equal(1f / 3f, _legacy.MaxMoveSeconds);
    }

    [Fact]
    public void NewCamera_WithNoRotation_LooksAlongTheWorldAxes()
    {
        var camera = new FreeFlyCamera(_legacy, new float3(0f, 47.1f, -10f));

        AssertVector(new float3(1f, 0f, 0f), camera.Right);
        AssertVector(new float3(0f, 1f, 0f), camera.Up);
        AssertVector(new float3(0f, 0f, 1f), camera.Forward);
    }

    [Fact]
    public void Axes_WithYawAndPitch_AreOrthonormal()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero, yaw: 37f, pitch: -52f);

        Assert.Equal(1f, camera.Right.Length(), 1e-5f);
        Assert.Equal(1f, camera.Up.Length(), 1e-5f);
        Assert.Equal(1f, camera.Forward.Length(), 1e-5f);
        Assert.Equal(0f, float3.Dot(camera.Right, camera.Up), 1e-5f);
        Assert.Equal(0f, float3.Dot(camera.Right, camera.Forward), 1e-5f);
        Assert.Equal(0f, float3.Dot(camera.Up, camera.Forward), 1e-5f);
        Assert.Equal(0f, camera.Right.Y);
        AssertVector(camera.Forward, float3.Cross(camera.Right, camera.Up));
    }

    [Fact]
    public void Look_CursorToTheRight_TurnsTheCameraToTheRight()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero);

        camera.Look(new float2(80f, 0f));

        Assert.Equal(10f, camera.Yaw, AngleTolerance);
        Assert.Equal(0f, camera.Pitch, AngleTolerance);
        Assert.True(camera.Forward.X > 0f);
    }

    [Fact]
    public void Look_CursorToTheLeft_GivesAYawFrom0To360()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero);

        camera.Look(new float2(-80f, 0f));

        Assert.Equal(350f, camera.Yaw, AngleTolerance);
    }

    [Fact]
    public void Look_CursorDown_TurnsTheCameraDown()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero);

        camera.Look(new float2(0f, 240f));

        Assert.Equal(30f, camera.Pitch, AngleTolerance);
        Assert.Equal(0f, camera.Yaw, AngleTolerance);
        Assert.True(camera.Forward.Y < 0f);
    }

    [Fact]
    public void Look_ManySmallMovementsDown_StopsAtTheMaximumPitch()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero);

        for (int i = 0; i < 20; i++)
        {
            camera.Look(new float2(0f, 40f));
        }

        Assert.Equal(80f, camera.Pitch, AngleTolerance);
        Assert.Equal(0f, camera.Yaw, AngleTolerance);
    }

    [Fact]
    public void Look_ManySmallMovementsUp_StopsAtTheMinimumPitch()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero);

        for (int i = 0; i < 20; i++)
        {
            camera.Look(new float2(0f, -40f));
        }

        Assert.Equal(-80f, camera.Pitch, AngleTolerance);
    }

    // The local Y axis of the legacy camera is not vertical at a pitch of 60 degrees.
    // Thus 1 degree of cursor movement changes the yaw by atan(tan(1°) / cos(60°)), about 2 degrees.
    [Fact]
    public void Look_HorizontalMovementAtAPitch_TurnsFasterAsTheLegacyCamera()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero, yaw: 0f, pitch: 60f);

        camera.Look(new float2(8f, 0f));

        double expectedYaw = Degrees(Math.Atan(Math.Tan(Radians(1.0)) / Math.Cos(Radians(60.0))));
        double expectedPitch = Degrees(Math.Asin(Math.Cos(Radians(1.0)) * Math.Sin(Radians(60.0))));
        Assert.Equal(expectedYaw, camera.Yaw, 1e-3);
        Assert.Equal(expectedPitch, camera.Pitch, 1e-3);
        Assert.Equal(2.0, camera.Yaw, 0.01);
    }

    // The legacy camera turns past the vertical in one frame if the cursor movement is large. Then the Euler angles
    // of the new rotation have a yaw that is 180 degrees different, and the pitch limit applies.
    [Fact]
    public void Look_TiltPastTheVertical_GivesTheResultOfTheLegacyCamera()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero, yaw: 0f, pitch: 75f);

        camera.Look(new float2(0f, 160f));
        (double unityYaw, double unityPitch, bool nearVertical) = UnityCameraEmulation.Frame(0.0, 75.0, 0.0, 160.0, _legacy);

        Assert.Equal(180f, camera.Yaw, AngleTolerance);
        Assert.Equal(80f, camera.Pitch, AngleTolerance);
        Assert.False(nearVertical);
        Assert.Equal(180.0, unityYaw, 1e-3);
        Assert.Equal(80.0, unityPitch, 1e-3);
    }

    // UnityCameraEmulation calculates the frame of the legacy script with the math of Unity.
    // The port gives the same yaw and pitch within 0.004 degrees. The test does not compare the frames that stop near the vertical.
    // There, Unity uses another formula (see the next test).
    [Theory]
    [InlineData("small movements")]
    [InlineData("large movements")]
    [InlineData("movements to the pitch limits")]
    public void Look_RandomFrames_GivesTheAnglesOfUnityWithin0004Degrees(string set)
    {
        var random = new Random(38);
        double maxYawError = 0;
        double maxPitchError = 0;
        int compared = 0;
        for (int i = 0; i < 20_000; i++)
        {
            (float yaw, float pitch, float cursorX, float cursorY) = RandomFrame(random, set);
            (double unityYaw, double unityPitch, bool nearVertical) = UnityCameraEmulation.Frame(yaw, pitch, cursorX, cursorY, _legacy);
            if (nearVertical)
            {
                continue;
            }

            var camera = new FreeFlyCamera(_legacy, float3.Zero, yaw, pitch);
            camera.Look(new float2(cursorX, cursorY));
            maxYawError = Math.Max(maxYawError, Math.Abs(AngleDifference(unityYaw, camera.Yaw)));
            maxPitchError = Math.Max(maxPitchError, Math.Abs(unityPitch - camera.Pitch));
            compared++;
        }

        Assert.True(compared > 19_000, $"The test compared only {compared} frames.");
        Assert.True(maxYawError <= 0.004, $"Yaw difference: {maxYawError} degrees.");
        Assert.True(maxPitchError <= 0.004, $"Pitch difference: {maxPitchError} degrees.");
    }

    // A known difference: the rotation stops 0.05 degree past the vertical (pitch 80, then a tilt of 10.05 degrees).
    // Unity uses the formula for the vertical and keeps the yaw. The port turns the yaw by 180 degrees.
    // Both give the pitch limit. Unity also turns the yaw for a rotation more than about 0.1 degree past the vertical.
    [Fact]
    public void Look_StopsWithinATenthOfADegreeOfTheVertical_TurnsTheYawUnlikeUnity()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero, yaw: 30f, pitch: 80f);

        camera.Look(new float2(0f, 80.4f));
        (double unityYaw, double unityPitch, bool nearVertical) = UnityCameraEmulation.Frame(30.0, 80.0, 0.0, 80.4, _legacy);

        Assert.True(nearVertical);
        Assert.Equal(30.0, unityYaw, 1e-3);
        Assert.Equal(80.0, unityPitch, 1e-3);
        Assert.Equal(210f, camera.Yaw, AngleTolerance);
        Assert.Equal(80f, camera.Pitch, AngleTolerance);
    }

    [Fact]
    public void Look_WithZeroMovement_KeepsTheAngles()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero, yaw: 33f, pitch: -21f);

        camera.Look(new float2(0f, 0f));

        Assert.Equal(33f, camera.Yaw, AngleTolerance);
        Assert.Equal(-21f, camera.Pitch, AngleTolerance);
    }

    [Fact]
    public void Move_Forward_MovesAtTheSpeed()
    {
        var camera = new FreeFlyCamera(_legacy, new float3(0f, 47.1f, -10f));

        camera.Move(new float3(0f, 0f, 1f), fast: false, seconds: 0.25f);

        AssertVector(new float3(0f, 47.1f, 0f), camera.Position);
    }

    [Fact]
    public void Move_Fast_MultipliesTheSpeed()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero);

        camera.Move(new float3(0f, 0f, 1f), fast: true, seconds: 0.25f);

        AssertVector(new float3(0f, 0f, 50f), camera.Position);
    }

    [Fact]
    public void Move_AfterATurnToTheRight_GoesAlongTheWorldXAxis()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero, yaw: 90f);

        camera.Move(new float3(0f, 0f, 1f), fast: false, seconds: 0.25f);

        AssertVector(new float3(10f, 0f, 0f), camera.Position);
    }

    // The legacy script moves the camera with Translate in local space. Thus the pitch changes all the directions.
    [Fact]
    public void Move_WithAPitch_GoesAlongTheLocalAxes()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero, yaw: 0f, pitch: 30f);
        float sin30 = MathF.Sin(float.DegreesToRadians(30f));
        float cos30 = MathF.Cos(float.DegreesToRadians(30f));

        camera.Move(new float3(0f, 0f, 1f), fast: false, seconds: 0.25f);
        AssertVector(new float3(0f, -10f * sin30, 10f * cos30), camera.Position);

        camera.Move(new float3(0f, 0f, -1f), fast: false, seconds: 0.25f);
        camera.Move(new float3(0f, 1f, 0f), fast: false, seconds: 0.25f);
        AssertVector(new float3(0f, 10f * cos30, 10f * sin30), camera.Position);
    }

    [Fact]
    public void Move_Diagonal_IsNotNormalized()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero);

        camera.Move(new float3(1f, 0f, 1f), fast: false, seconds: 0.25f);

        AssertVector(new float3(10f, 0f, 10f), camera.Position);
    }

    // Unity limits Time.deltaTime to 1/3 second. Thus a frame of 2 seconds moves the camera for 1/3 second only.
    [Fact]
    public void Move_LongFrame_MovesOnlyForTheMaximumDuration()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero);

        camera.Move(new float3(0f, 0f, 1f), fast: false, seconds: 2f);
        camera.Move(new float3(1f, 0f, 0f), fast: true, seconds: 1f / 3f);

        AssertVector(new float3(200f / 3f, 0f, 40f / 3f), camera.Position);
    }

    [Fact]
    public void Move_NegativeDuration_Throws()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Move(new float3(0f, 0f, 1f), fast: false, seconds: -0.1f));
    }

    [Fact]
    public void ViewMatrix_PutsTheCameraAtTheOriginAndLooksAlongZ()
    {
        var camera = new FreeFlyCamera(_legacy, new float3(5f, 20f, -3f), yaw: 0f, pitch: 0f);
        float4x4 view = camera.CreateViewMatrix();

        AssertVector(float3.Zero, TransformPoint(new float3(5f, 20f, -3f), view));
        AssertVector(new float3(0f, 0f, 10f), TransformPoint(new float3(5f, 20f, 7f), view));
        AssertVector(new float3(2f, 0f, 0f), TransformPoint(new float3(7f, 20f, -3f), view));
        AssertVector(new float3(0f, 4f, 0f), TransformPoint(new float3(5f, 24f, -3f), view));
    }

    [Fact]
    public void ViewMatrix_WithYawAndPitch_UsesTheAxesOfTheCamera()
    {
        var camera = new FreeFlyCamera(_legacy, new float3(-12f, 30f, 8f), yaw: 135f, pitch: 40f);
        float4x4 view = camera.CreateViewMatrix();
        float3 point = camera.Position + (camera.Forward * 6f) + (camera.Right * 2f) - (camera.Up * 3f);

        AssertVector(new float3(2f, -3f, 6f), TransformPoint(point, view));
    }

    // Brutal.Numerics: float4.Transform uses the row-vector convention, as the camera matrices.
    [Fact]
    public void ViewMatrix_WithFloat4Transform_GivesTheSameResultAsTheRowVectorProduct()
    {
        var camera = new FreeFlyCamera(_legacy, new float3(3f, 50f, -20f), yaw: 210f, pitch: -15f);
        float4x4 view = camera.CreateViewMatrix();
        var point = new float3(1f, 2f, 3f);

        float4 transformed = float4.Transform(point, view);

        AssertVector(TransformPoint(point, view), transformed.XYZ);
        Assert.Equal(1f, transformed.W, 1e-5f);
    }

    [Fact]
    public void ViewAndProjection_PointInFrontOfTheCamera_IsInTheCenterOfTheView()
    {
        var camera = new FreeFlyCamera(_legacy, new float3(0f, 47.1f, -10f));
        float4x4 viewProjection = camera.CreateViewMatrix() * new CameraLens().CreateProjectionMatrix(16f / 9f);

        float4 clip = TransformRow(new float3(0f, 47.1f, 90f), viewProjection);

        Assert.Equal(0f, clip.X / clip.W, 1e-5f);
        Assert.Equal(0f, clip.Y / clip.W, 1e-5f);
        Assert.InRange(clip.Z / clip.W, 0f, 1f);
        Assert.Equal(100f, clip.W, 1e-3f);
    }

    [Fact]
    public void ViewAndProjection_PointAboveAndToTheRight_HasNegativeYAndPositiveX()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero, yaw: 270f, pitch: 20f);
        float4x4 viewProjection = camera.CreateViewMatrix() * new CameraLens().CreateProjectionMatrix(1f);
        float3 point = (camera.Forward * 50f) + (camera.Up * 5f) + (camera.Right * 5f);

        float4 clip = TransformRow(point, viewProjection);

        Assert.True(clip.X / clip.W > 0f);
        Assert.True(clip.Y / clip.W < 0f);
    }

    [Theory]
    [InlineData(-90.5f, 80f)]
    [InlineData(-80f, 90.5f)]
    [InlineData(10f, -10f)]
    [InlineData(float.NaN, 80f)]
    public void Constructor_WrongPitchLimits_Throws(float minPitch, float maxPitch)
    {
        var settings = new FreeFlyCameraSettings { MinPitch = minPitch, MaxPitch = maxPitch };

        Assert.Throws<ArgumentOutOfRangeException>(() => new FreeFlyCamera(settings, float3.Zero));
    }

    [Theory]
    [InlineData(-1f, 5f, 0.125f)]
    [InlineData(40f, -5f, 0.125f)]
    [InlineData(float.PositiveInfinity, 5f, 0.125f)]
    [InlineData(40f, 5f, float.NaN)]
    public void Constructor_WrongControlValue_Throws(float speed, float multiplier, float sensitivity)
    {
        var settings = new FreeFlyCameraSettings { Speed = speed, SpeedMultiplier = multiplier, Sensitivity = sensitivity };

        Assert.Throws<ArgumentOutOfRangeException>(() => new FreeFlyCamera(settings, float3.Zero));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Constructor_WrongMaximumMoveDuration_Throws(float maxMoveSeconds)
    {
        var settings = new FreeFlyCameraSettings { MaxMoveSeconds = maxMoveSeconds };

        Assert.Throws<ArgumentOutOfRangeException>(() => new FreeFlyCamera(settings, float3.Zero));
    }

    [Fact]
    public void Constructor_PositionNotFinite_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FreeFlyCamera(_legacy, new float3(0f, float.NaN, 0f)));
    }

    [Fact]
    public void Constructor_PitchOutOfTheLimits_GivesTheLimit()
    {
        var camera = new FreeFlyCamera(_legacy, float3.Zero, yaw: -30f, pitch: 85f);

        Assert.Equal(80f, camera.Pitch);
        Assert.Equal(330f, camera.Yaw, AngleTolerance);
    }

    // Row-vector convention: p × M, with p = (x, y, z, 1).
    internal static float4 TransformRow(float3 point, float4x4 matrix) =>
        (matrix.X * point.X) + (matrix.Y * point.Y) + (matrix.Z * point.Z) + matrix.W;

    private static float3 TransformPoint(float3 point, float4x4 matrix)
    {
        float4 result = TransformRow(point, matrix);
        return result.XYZ / result.W;
    }

    private static void AssertVector(float3 expected, float3 actual)
    {
        Assert.Equal(expected.X, actual.X, PositionTolerance);
        Assert.Equal(expected.Y, actual.Y, PositionTolerance);
        Assert.Equal(expected.Z, actual.Z, PositionTolerance);
    }

    // Random frames from a seed that does not change. The values are float values, as in the app.
    private static (float Yaw, float Pitch, float CursorX, float CursorY) RandomFrame(Random random, string set)
    {
        float yaw = (float)(random.NextDouble() * 360.0);
        switch (set)
        {
            case "small movements":
                return (yaw, (float)((random.NextDouble() * 160.0) - 80.0), random.Next(-50, 51), random.Next(-50, 51));
            case "large movements":
                return (yaw, (float)((random.NextDouble() * 160.0) - 80.0), random.Next(-3000, 3001), random.Next(-3000, 3001));
            default:
                // A pitch from 70 to 80 degrees (often the limit), and a vertical movement to the limit, up or down.
                float sign = random.Next(2) == 0 ? -1f : 1f;
                float pitch = random.Next(2) == 0 ? 80f : (float)(70.0 + (random.NextDouble() * 10.0));
                return (yaw, sign * pitch, random.Next(-120, 121), sign * random.Next(0, 241));
        }
    }

    private static double AngleDifference(double a, double b)
    {
        double difference = (a - b) % 360.0;
        return difference switch
        {
            > 180.0 => difference - 360.0,
            < -180.0 => difference + 360.0,
            _ => difference,
        };
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180.0;

    private static double Degrees(double radians) => radians * 180.0 / Math.PI;
}
