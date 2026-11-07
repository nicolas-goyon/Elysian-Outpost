// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;

namespace ElysianOutpost.Core.Cameras;

/// <summary>
/// The six planes of the camera view (#50). The renderer does not draw a chunk whose box is outside these planes.
/// </summary>
/// <remarks>
/// <para>
/// The planes come from the view-projection matrix (the method of Gribb and Hartmann). The matrices of the app use row vectors:
/// clip = (x, y, z, 1) × matrix. Thus each clip coordinate uses one column of the matrix.
/// A point is in the view if −w ≤ x ≤ w, −w ≤ y ≤ w and 0 ≤ z ≤ w. The Vulkan depth goes from 0 to 1.
/// </para>
/// <para>
/// The box test is conservative: it can say that a box near a corner of the view intersects the view, but it never says
/// that a box in the view is outside. Thus the renderer can draw a chunk that the camera does not see, but it never hides a visible chunk.
/// </para>
/// </remarks>
public readonly struct ViewFrustum
{
    // Each plane is (a, b, c, d): a point p is on the inner side if a × p.x + b × p.y + c × p.z + d ≥ 0.
    private readonly float4 _left;
    private readonly float4 _right;
    private readonly float4 _bottom;
    private readonly float4 _top;
    private readonly float4 _near;
    private readonly float4 _far;

    private ViewFrustum(float4 left, float4 right, float4 bottom, float4 top, float4 near, float4 far)
    {
        _left = left;
        _right = right;
        _bottom = bottom;
        _top = top;
        _near = near;
        _far = far;
    }

    /// <summary>
    /// Makes the planes of the view from a view-projection matrix of the app.
    /// </summary>
    /// <param name="viewProjection">The view matrix multiplied by the projection matrix (row vectors).</param>
    /// <returns>The planes of the view.</returns>
    public static ViewFrustum FromViewProjection(float4x4 viewProjection)
    {
        // The column i of the matrix gives the clip coordinate i: (X.i, Y.i, Z.i, W.i).
        float4 x = new(viewProjection.X.X, viewProjection.Y.X, viewProjection.Z.X, viewProjection.W.X);
        float4 y = new(viewProjection.X.Y, viewProjection.Y.Y, viewProjection.Z.Y, viewProjection.W.Y);
        float4 z = new(viewProjection.X.Z, viewProjection.Y.Z, viewProjection.Z.Z, viewProjection.W.Z);
        float4 w = new(viewProjection.X.W, viewProjection.Y.W, viewProjection.Z.W, viewProjection.W.W);
        return new ViewFrustum(w + x, w - x, w + y, w - y, z, w - z);
    }

    /// <summary>
    /// Gets a value that tells if an axis-aligned box is in the view or intersects it.
    /// </summary>
    /// <param name="min">The lowest corner of the box.</param>
    /// <param name="max">The highest corner of the box.</param>
    /// <returns><c>false</c> if the box is fully on the outer side of one plane. Otherwise <c>true</c>.</returns>
    public bool Intersects(float3 min, float3 max) =>
        HasCornerInside(_left, min, max)
        && HasCornerInside(_right, min, max)
        && HasCornerInside(_bottom, min, max)
        && HasCornerInside(_top, min, max)
        && HasCornerInside(_near, min, max)
        && HasCornerInside(_far, min, max);

    // The corner of the box that is the most on the inner side of the plane. If this corner is outside, all the box is outside.
    private static bool HasCornerInside(float4 plane, float3 min, float3 max)
    {
        float x = plane.X >= 0f ? max.X : min.X;
        float y = plane.Y >= 0f ? max.Y : min.Y;
        float z = plane.Z >= 0f ? max.Z : min.Z;
        return (plane.X * x) + (plane.Y * y) + (plane.Z * z) + plane.W >= 0f;
    }
}
