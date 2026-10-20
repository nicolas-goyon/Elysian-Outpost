// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace ElysianOutpost.Core.Tests.Meshing;

/// <summary>
/// The six directions of a voxel face. The direction of a face goes from its voxel to the neighbor voxel.
/// </summary>
internal enum FaceDirection
{
    NegativeX,
    PositiveX,
    NegativeY,
    PositiveY,
    NegativeZ,
    PositiveZ,
}

/// <summary>
/// Gives the axis, the sign and the vectors of a <see cref="FaceDirection"/>.
/// </summary>
internal static class FaceDirections
{
    /// <summary>
    /// Gets the six directions.
    /// </summary>
    public static IReadOnlyList<FaceDirection> All { get; } = Enum.GetValues<FaceDirection>();

    /// <summary>
    /// Gets the axis of the direction: 0 for X, 1 for Y and 2 for Z.
    /// </summary>
    public static int Axis(this FaceDirection direction) => (int)direction / 2;

    /// <summary>
    /// Gets a value that tells if the direction goes to the higher coordinates.
    /// </summary>
    public static bool IsPositive(this FaceDirection direction) => (int)direction % 2 == 1;

    /// <summary>
    /// Gets the direction of an axis and a sign.
    /// </summary>
    public static FaceDirection FromAxis(int axis, bool positive) => (FaceDirection)((axis * 2) + (positive ? 1 : 0));

    /// <summary>
    /// Gets the unit vector of the direction. This is the correct normal of a face in this direction.
    /// </summary>
    public static Vector3 UnitVector(this FaceDirection direction)
    {
        float sign = direction.IsPositive() ? 1f : -1f;
        return direction.Axis() switch
        {
            0 => new Vector3(sign, 0f, 0f),
            1 => new Vector3(0f, sign, 0f),
            _ => new Vector3(0f, 0f, sign),
        };
    }

    /// <summary>
    /// Gets a short label of the direction, for example "+X".
    /// </summary>
    public static string Label(this FaceDirection direction) => $"{(direction.IsPositive() ? '+' : '-')}{"XYZ"[direction.Axis()]}";
}
