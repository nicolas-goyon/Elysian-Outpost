// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;

namespace ElysianOutpost.Core.Lighting;

/// <summary>
/// The light of a scene: one directional light and one ambient light. All the colors are linear values.
/// </summary>
public sealed record SceneLighting
{
    /// <summary>
    /// Gets the unit vector from a surface to the directional light, in world axes.
    /// </summary>
    public required float3 DirectionToLight { get; init; }

    /// <summary>
    /// Gets the color of the directional light, multiplied by its intensity.
    /// </summary>
    public required float3 LightColor { get; init; }

    /// <summary>
    /// Gets the ambient light, which changes with the direction of the normal.
    /// </summary>
    public required AmbientProbe Ambient { get; init; }

    /// <summary>
    /// Gets an approximation of the light of the legacy scene (<c>SampleScene.unity</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The directional light of the scene has the rotation (50, -30, 0), the intensity 2, the color white and a color temperature of 5000 K.
    /// The linear color of 5000 K is approximately (1, 0.79, 0.63), from the CIE 1931 chromaticity of a black body at this temperature.
    /// </para>
    /// <para>
    /// The ambient light comes from the default procedural skybox of the scene. See <see cref="AmbientProbe.LegacyDefaultSkybox"/>.
    /// </para>
    /// </remarks>
    public static SceneLighting LegacySampleScene { get; } = new()
    {
        DirectionToLight = -UnityForward(50f, -30f),
        LightColor = new float3(1f, 0.7926f, 0.6294f) * 2f,
        Ambient = AmbientProbe.LegacyDefaultSkybox,
    };

    /// <summary>
    /// Gives the forward direction (the local Z axis) of a Unity object with the Euler angles (x, y, 0).
    /// For a directional light, this is the direction in which the light goes.
    /// </summary>
    /// <param name="eulerX">The Euler angle X, in degrees. A positive value turns the forward direction down.</param>
    /// <param name="eulerY">The Euler angle Y, in degrees. A positive value turns the forward direction to the right.</param>
    /// <returns>The unit forward vector, in the world axes of the legacy project: X to the right, Y up and Z forward.</returns>
    public static float3 UnityForward(float eulerX, float eulerY)
    {
        (float sinX, float cosX) = MathF.SinCos(float.DegreesToRadians(eulerX));
        (float sinY, float cosY) = MathF.SinCos(float.DegreesToRadians(eulerY));
        return new float3(cosX * sinY, -sinX, cosX * cosY);
    }
}
