// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;

namespace ElysianOutpost.Core.Cameras;

/// <summary>
/// A camera that flies freely in the world. It moves and turns as the camera of the legacy project
/// (<c>CameraMovements</c> in <c>Base/Camera/CameraMouvements.cs</c>).
/// </summary>
/// <remarks>
/// <para>
/// The world axes are the axes of the legacy project: X points to the right, Y points up and Z points forward (left-handed).
/// The mesher and the terrain generation use these axes.
/// </para>
/// <para>
/// The camera has no roll. The yaw turns the camera around the world Y axis. A positive yaw turns the camera to the right.
/// The pitch turns the camera around its X axis. A positive pitch turns the camera down.
/// These angles are the Euler angles Y and X of the legacy camera.
/// </para>
/// <para>
/// The matrices use the row-vector convention of <c>System.Numerics</c>: a point p changes into p × M.
/// <c>float4.Transform</c> and <c>float3.Transform</c> use this convention.
/// The operator <c>float4x4 * float4</c> of <c>Brutal.Numerics</c> uses the column-vector convention. Do not use it with these matrices.
/// A GLSL shader reads the memory of a matrix as the transposed matrix. Thus the shader uses M × p.
/// </para>
/// </remarks>
public sealed class FreeFlyCamera
{
    private const float FullTurn = 360f;

    /// <summary>
    /// Initializes a new instance of the <see cref="FreeFlyCamera"/> class.
    /// </summary>
    /// <param name="settings">The control values of the camera.</param>
    /// <param name="position">The start position of the camera, in world units.</param>
    /// <param name="yaw">The start yaw, in degrees.</param>
    /// <param name="pitch">The start pitch, in degrees. The camera limits it to the pitch range of the settings.</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A value of the settings is not correct, or an angle or a coordinate is not finite.
    /// The speed and the speed multiplier must be zero or more. The sensitivity must be finite.
    /// The pitch limits must be from -90 to 90 degrees, and the minimum must not be more than the maximum.
    /// The maximum duration of one movement must be more than zero.
    /// </exception>
    public FreeFlyCamera(FreeFlyCameraSettings settings, float3 position, float yaw = 0f, float pitch = 0f)
    {
        ArgumentNullException.ThrowIfNull(settings);
        RequireZeroOrMore(settings.Speed, nameof(settings.Speed));
        RequireZeroOrMore(settings.SpeedMultiplier, nameof(settings.SpeedMultiplier));
        RequireFinite(settings.Sensitivity, nameof(settings.Sensitivity));
        if (!(settings.MaxMoveSeconds > 0f) || !float.IsFinite(settings.MaxMoveSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(settings), settings.MaxMoveSeconds, "The maximum duration of one movement must be a finite value more than zero.");
        }

        if (!(settings.MinPitch >= -90f && settings.MinPitch <= settings.MaxPitch && settings.MaxPitch <= 90f))
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                $"The pitch limits must be from -90 to 90 degrees, with the minimum not more than the maximum. Minimum: {settings.MinPitch}. Maximum: {settings.MaxPitch}.");
        }

        RequireFinite(position.X, nameof(position));
        RequireFinite(position.Y, nameof(position));
        RequireFinite(position.Z, nameof(position));
        RequireFinite(yaw, nameof(yaw));
        RequireFinite(pitch, nameof(pitch));

        Settings = settings;
        Position = position;
        Yaw = NormalizeYaw(yaw);
        Pitch = Math.Clamp(pitch, settings.MinPitch, settings.MaxPitch);
    }

    /// <summary>
    /// Gets the control values of the camera.
    /// </summary>
    public FreeFlyCameraSettings Settings { get; }

    /// <summary>
    /// Gets the position of the camera, in world units.
    /// </summary>
    public float3 Position { get; private set; }

    /// <summary>
    /// Gets the yaw of the camera, in degrees, from 0 (included) to 360 (not included).
    /// </summary>
    public float Yaw { get; private set; }

    /// <summary>
    /// Gets the pitch of the camera, in degrees, in the pitch range of the settings.
    /// </summary>
    public float Pitch { get; private set; }

    /// <summary>
    /// Gets the unit vector that points to the right of the view, in world axes. It is always horizontal.
    /// </summary>
    public float3 Right
    {
        get
        {
            (float sinYaw, float cosYaw) = MathF.SinCos(float.DegreesToRadians(Yaw));
            return new float3(cosYaw, 0f, -sinYaw);
        }
    }

    /// <summary>
    /// Gets the unit vector that points to the top of the view, in world axes.
    /// </summary>
    public float3 Up
    {
        get
        {
            (float sinYaw, float cosYaw) = MathF.SinCos(float.DegreesToRadians(Yaw));
            (float sinPitch, float cosPitch) = MathF.SinCos(float.DegreesToRadians(Pitch));
            return new float3(sinPitch * sinYaw, cosPitch, sinPitch * cosYaw);
        }
    }

    /// <summary>
    /// Gets the unit vector that points in the direction of the view, in world axes.
    /// </summary>
    public float3 Forward
    {
        get
        {
            (float sinYaw, float cosYaw) = MathF.SinCos(float.DegreesToRadians(Yaw));
            (float sinPitch, float cosPitch) = MathF.SinCos(float.DegreesToRadians(Pitch));
            return new float3(cosPitch * sinYaw, -sinPitch, cosPitch * cosYaw);
        }
    }

    /// <summary>
    /// Turns the camera for a movement of the cursor, as the legacy camera does in each frame.
    /// </summary>
    /// <param name="cursorDelta">
    /// The movement of the cursor since the previous frame, in pixels, with the axes of the window: X to the right and Y down.
    /// </param>
    /// <remarks>
    /// <para>
    /// The legacy camera turns around its local Y axis for the X movement, then around its new local X axis for the Y movement.
    /// Then it sets the roll to zero and limits the pitch.
    /// This method calculates the same angles within 0.004 degrees. A cursor movement to the right turns the camera to the right.
    /// A cursor movement down turns the camera down.
    /// </para>
    /// <para>
    /// A rotation past the vertical turns the yaw by 180 degrees, in the legacy camera and in this method.
    /// One case is different: a rotation that stops within about 0.1 degree of the vertical.
    /// There, Unity calculates the Euler angles with another formula and keeps the yaw. This method turns the yaw by 180 degrees.
    /// </para>
    /// <para>
    /// The local Y axis is not vertical when the pitch is not zero. Thus the yaw changes more than the cursor movement
    /// when the camera looks up or down. For a small movement, the change is the movement divided by the cosine of the pitch.
    /// For example, at a pitch of 60 degrees, the yaw changes two times faster than at a pitch of 0.
    /// </para>
    /// </remarks>
    public void Look(float2 cursorDelta)
    {
        float turn = float.DegreesToRadians(cursorDelta.X * Settings.Sensitivity);
        float tilt = float.DegreesToRadians(cursorDelta.Y * Settings.Sensitivity);
        if (!float.IsFinite(turn) || !float.IsFinite(tilt))
        {
            return;
        }

        (float sinTurn, float cosTurn) = MathF.SinCos(turn);
        (float sinTilt, float cosTilt) = MathF.SinCos(tilt);
        (float sinPitch, float cosPitch) = MathF.SinCos(float.DegreesToRadians(Pitch));

        // The new view direction before the yaw rotation: RotationX(pitch) × RotationY(turn) × RotationX(tilt) × (0, 0, 1).
        float x = cosTilt * sinTurn;
        float y = (-sinTilt * cosPitch) - (cosTilt * cosTurn * sinPitch);
        float z = (-sinTilt * sinPitch) + (cosTilt * cosTurn * cosPitch);

        // The yaw rotation adds to the horizontal angle of this direction. The Euler angles of the direction give the new pitch.
        float pitch = float.RadiansToDegrees(MathF.Asin(Math.Clamp(-y, -1f, 1f)));
        Yaw = NormalizeYaw(Yaw + float.RadiansToDegrees(MathF.Atan2(x, z)));
        Pitch = Math.Clamp(pitch, Settings.MinPitch, Settings.MaxPitch);
    }

    /// <summary>
    /// Moves the camera along its local axes, as the legacy camera does in each frame.
    /// </summary>
    /// <param name="direction">
    /// The movement input on the local axes: X to the right, Y up and Z forward. Each component is usually -1, 0 or 1.
    /// The camera does not normalize this vector. Thus a diagonal movement is faster, as in the legacy project.
    /// </param>
    /// <param name="fast"><c>true</c> to multiply the speed by the speed multiplier.</param>
    /// <param name="seconds">
    /// The duration of the movement, in seconds, usually the time of the frame.
    /// The camera limits it to <see cref="FreeFlyCameraSettings.MaxMoveSeconds"/>, as Unity limits <c>Time.deltaTime</c>.
    /// </param>
    /// <remarks>
    /// The local axes include the pitch. Thus the forward movement goes in the direction of the view,
    /// and the up movement goes to the top of the view, not straight up.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The duration is less than zero or not finite.</exception>
    public void Move(float3 direction, bool fast, float seconds)
    {
        RequireZeroOrMore(seconds, nameof(seconds));
        float duration = Math.Min(seconds, Settings.MaxMoveSeconds);
        float distance = Settings.Speed * (fast ? Settings.SpeedMultiplier : 1f) * duration;
        float3 movement = (Right * direction.X) + (Up * direction.Y) + (Forward * direction.Z);
        Position += movement * distance;
    }

    /// <summary>
    /// Makes the view matrix of the camera.
    /// </summary>
    /// <returns>The view matrix, in the row-vector convention.</returns>
    /// <remarks>
    /// The view space has the axes of the view: X to the right, Y up and Z forward (left-handed).
    /// The camera is at the origin of the view space. <see cref="CameraLens.CreateProjectionMatrix"/> uses this view space.
    /// </remarks>
    public float4x4 CreateViewMatrix()
    {
        float3 right = Right;
        float3 up = Up;
        float3 forward = Forward;
        float3 position = Position;
        return new float4x4(
            right.X, up.X, forward.X, 0f,
            right.Y, up.Y, forward.Y, 0f,
            right.Z, up.Z, forward.Z, 0f,
            -float3.Dot(right, position), -float3.Dot(up, position), -float3.Dot(forward, position), 1f);
    }

    private static float NormalizeYaw(float yaw)
    {
        float normalized = yaw % FullTurn;
        if (normalized < 0f)
        {
            normalized += FullTurn;
        }

        // A very small negative value plus 360 can round to 360.
        return normalized >= FullTurn ? 0f : normalized;
    }

    private static void RequireFinite(float value, string name)
    {
        if (!float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "The value must be finite.");
        }
    }

    private static void RequireZeroOrMore(float value, string name)
    {
        if (!(value >= 0f) || !float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "The value must be finite and zero or more.");
        }
    }
}
