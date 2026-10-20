// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;

namespace ElysianOutpost.Core.Cameras;

/// <summary>
/// Gives the perspective values of a camera: the vertical field of view and the distances of the clip planes.
/// </summary>
/// <remarks>
/// The default values are the values of the camera in the legacy scene (<c>SampleScene.unity</c>).
/// </remarks>
public sealed record CameraLens
{
    /// <summary>
    /// Gets the vertical field of view, in degrees.
    /// </summary>
    public float VerticalFieldOfView { get; init; } = 60f;

    /// <summary>
    /// Gets the distance from the camera to the near clip plane, in units.
    /// </summary>
    public float NearPlane { get; init; } = 0.3f;

    /// <summary>
    /// Gets the distance from the camera to the far clip plane, in units.
    /// </summary>
    public float FarPlane { get; init; } = 1000f;

    /// <summary>
    /// Makes the perspective projection matrix for Vulkan.
    /// </summary>
    /// <param name="aspectRatio">The width of the view divided by its height.</param>
    /// <returns>The projection matrix, in the row-vector convention of <see cref="FreeFlyCamera"/>.</returns>
    /// <remarks>
    /// The matrix changes the view space of <see cref="FreeFlyCamera.CreateViewMatrix"/> into the clip space of Vulkan.
    /// After the division by W, the depth is 0 on the near plane and 1 on the far plane.
    /// The Y axis of the clip space points down. Thus a point above the center of the view gets a negative Y value.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The aspect ratio is not more than zero, or a value of the lens is not correct.
    /// The field of view must be more than 0 and less than 180 degrees. The far plane must be farther than the near plane.
    /// </exception>
    public float4x4 CreateProjectionMatrix(float aspectRatio)
    {
        if (!(aspectRatio > 0f) || !float.IsFinite(aspectRatio))
        {
            throw new ArgumentOutOfRangeException(nameof(aspectRatio), aspectRatio, "The aspect ratio must be a finite value more than zero.");
        }

        if (!(VerticalFieldOfView > 0f && VerticalFieldOfView < 180f))
        {
            throw new ArgumentOutOfRangeException(nameof(VerticalFieldOfView), VerticalFieldOfView, "The field of view must be more than 0 and less than 180 degrees.");
        }

        if (!(NearPlane > 0f) || !float.IsFinite(FarPlane) || !(FarPlane > NearPlane))
        {
            throw new ArgumentOutOfRangeException(nameof(FarPlane), FarPlane, $"The far plane must be a finite distance more than the near plane ({NearPlane}), and the near plane must be more than zero.");
        }

        float scale = 1f / MathF.Tan(float.DegreesToRadians(VerticalFieldOfView) / 2f);
        float depthScale = FarPlane / (FarPlane - NearPlane);

        // Row-vector convention: clip = (x × scale / aspect, -y × scale, (z - near) × depthScale, z).
        return new float4x4(
            scale / aspectRatio, 0f, 0f, 0f,
            0f, -scale, 0f, 0f,
            0f, 0f, depthScale, 1f,
            0f, 0f, -NearPlane * depthScale, 0f);
    }
}
