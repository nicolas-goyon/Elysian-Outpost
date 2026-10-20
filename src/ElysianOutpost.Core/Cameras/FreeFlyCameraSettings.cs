// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Cameras;

/// <summary>
/// Gives the control values of a <see cref="FreeFlyCamera"/>: the speed, the speed multiplier, the mouse sensitivity and the pitch limits.
/// </summary>
/// <remarks>
/// The default values are the values of the camera in the legacy scene (<c>SampleScene.unity</c>).
/// The scene changes two default values of the legacy script: the speed from 20 to 40, and the speed multiplier from 2 to 5.
/// </remarks>
public sealed record FreeFlyCameraSettings
{
    /// <summary>
    /// Gets the speed of the camera, in units per second. One unit is the size of one voxel.
    /// </summary>
    public float Speed { get; init; } = 40f;

    /// <summary>
    /// Gets the value that multiplies the speed when the player holds a speed key.
    /// </summary>
    public float SpeedMultiplier { get; init; } = 5f;

    /// <summary>
    /// Gets the rotation of the camera for one pixel of cursor movement, in degrees.
    /// </summary>
    public float Sensitivity { get; init; } = 0.125f;

    /// <summary>
    /// Gets the minimum pitch, in degrees. A negative pitch turns the view up.
    /// </summary>
    public float MinPitch { get; init; } = -80f;

    /// <summary>
    /// Gets the maximum pitch, in degrees. A positive pitch turns the view down.
    /// </summary>
    public float MaxPitch { get; init; } = 80f;

    /// <summary>
    /// Gets the maximum duration of one movement, in seconds. A longer frame moves the camera only for this duration.
    /// </summary>
    /// <remarks>
    /// The legacy movement uses <c>Time.deltaTime</c>, and Unity limits it to <c>Time.maximumDeltaTime</c>: 1/3 second by default.
    /// </remarks>
    public float MaxMoveSeconds { get; init; } = 1f / 3f;
}
