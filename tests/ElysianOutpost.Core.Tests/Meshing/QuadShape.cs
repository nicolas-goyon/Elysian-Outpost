// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using ElysianOutpost.Core.Meshing;

namespace ElysianOutpost.Core.Tests.Meshing;

/// <summary>
/// The shape of a quad on the voxel grid: its direction, its plane, the rectangle that it covers and its block type.
/// </summary>
/// <remarks>
/// <para>
/// U and V are the two axes of the plane, in the order X, Y, Z. For example, a quad in a Y direction has U = X and V = Z.
/// The rectangle covers the cells from <see cref="MinU"/> to <see cref="MaxU"/> - 1 and from <see cref="MinV"/> to <see cref="MaxV"/> - 1.
/// </para>
/// <para>
/// The direction of a quad comes from its winding order: it is the direction of (Vertex1 - Vertex0) x (Vertex2 - Vertex0).
/// The simple mesh of <c>Chunk.ToMesh</c> uses the same rule: the normal of each of its quads has this direction.
/// The legacy project draws the triangles (0, 1, 2) and (2, 3, 0) of each quad, and computes the normals from them.
/// </para>
/// </remarks>
/// <param name="Direction">The direction of the faces of the quad.</param>
/// <param name="Plane">The coordinate of the quad on the axis of its direction.</param>
/// <param name="MinU">The lowest U coordinate of the quad.</param>
/// <param name="MaxU">The highest U coordinate of the quad.</param>
/// <param name="MinV">The lowest V coordinate of the quad.</param>
/// <param name="MaxV">The highest V coordinate of the quad.</param>
/// <param name="BlockType">The block type of the quad.</param>
internal readonly record struct QuadShape(FaceDirection Direction, int Plane, int MinU, int MaxU, int MinV, int MaxV, uint BlockType)
{
    /// <summary>
    /// Gets the number of voxel faces that the quad covers.
    /// </summary>
    public int Area => (MaxU - MinU) * (MaxV - MinV);

    /// <summary>
    /// Reads the shape of a quad.
    /// </summary>
    /// <param name="quad">The quad.</param>
    /// <param name="shape">The shape of the quad, if the quad is correct.</param>
    /// <param name="problem">The problem of the quad, if the quad is not correct.</param>
    /// <returns>
    /// <c>true</c> if the quad is a rectangle on the voxel grid, with its edges parallel to two axes.
    /// <c>false</c> if not, for example for a quad with a vertex between two grid points, or with an edge of length 0.
    /// </returns>
    public static bool TryCreate(MeshQuad quad, out QuadShape shape, out string problem)
    {
        ArgumentNullException.ThrowIfNull(quad);
        shape = default;
        Vector3[] vertices = [quad.Vertex0, quad.Vertex1, quad.Vertex2, quad.Vertex3];
        foreach (Vector3 vertex in vertices)
        {
            if (!IsInteger(vertex.X) || !IsInteger(vertex.Y) || !IsInteger(vertex.Z))
            {
                problem = $"The vertex {vertex} is not on the voxel grid.";
                return false;
            }
        }

        // A parallelogram has Vertex2 = Vertex0 + edge1 + edge2.
        Vector3 edge1 = quad.Vertex1 - quad.Vertex0;
        Vector3 edge2 = quad.Vertex3 - quad.Vertex0;
        if (quad.Vertex2 != quad.Vertex1 + edge2)
        {
            problem = $"The vertices {quad.Vertex0}, {quad.Vertex1}, {quad.Vertex2} and {quad.Vertex3} are not the corners of a parallelogram in this order.";
            return false;
        }

        int axis1 = OnlyAxis(edge1);
        int axis2 = OnlyAxis(edge2);
        if (axis1 < 0 || axis2 < 0 || axis1 == axis2)
        {
            problem = $"The edges {edge1} and {edge2} are not parallel to two different axes.";
            return false;
        }

        // The two edges are perpendicular. Thus their cross product is parallel to the third axis.
        int axis = 3 - axis1 - axis2;
        float side = Component(Vector3.Cross(edge1, edge2), axis);
        (int uAxis, int vAxis) = PlaneAxes(axis);
        shape = new QuadShape(
            FaceDirections.FromAxis(axis, side > 0f),
            (int)Component(quad.Vertex0, axis),
            (int)vertices.Min(vertex => Component(vertex, uAxis)),
            (int)vertices.Max(vertex => Component(vertex, uAxis)),
            (int)vertices.Min(vertex => Component(vertex, vAxis)),
            (int)vertices.Max(vertex => Component(vertex, vAxis)),
            quad.VoxelID);
        problem = string.Empty;
        return true;
    }

    /// <summary>
    /// Reads the shape of a quad.
    /// </summary>
    /// <param name="quad">The quad.</param>
    /// <returns>The shape of the quad.</returns>
    /// <exception cref="InvalidDataException">The quad is not a rectangle on the voxel grid. See <see cref="TryCreate"/>.</exception>
    public static QuadShape From(MeshQuad quad) =>
        TryCreate(quad, out QuadShape shape, out string problem) ? shape : throw new InvalidDataException(problem);

    /// <summary>
    /// Gives the voxel faces that the quad covers.
    /// </summary>
    /// <returns>One face for each cell of the rectangle. The voxel of each face is on the inner side of the plane.</returns>
    public IEnumerable<Face> Faces()
    {
        int axis = Direction.Axis();
        (int uAxis, int vAxis) = PlaneAxes(axis);

        // A face in a positive direction is on the high side of its voxel. Thus the voxel is before the plane.
        int depth = Direction.IsPositive() ? Plane - 1 : Plane;
        var position = new int[3];
        position[axis] = depth;
        for (int u = MinU; u < MaxU; u++)
        {
            for (int v = MinV; v < MaxV; v++)
            {
                position[uAxis] = u;
                position[vAxis] = v;
                yield return new Face(position[0], position[1], position[2], Direction);
            }
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        (int uAxis, int vAxis) = PlaneAxes(Direction.Axis());
        const string axisNames = "xyz";
        return $"{Direction.Label()} quad at {axisNames[Direction.Axis()]} = {Plane}, {axisNames[uAxis]} = {MinU}..{MaxU}, {axisNames[vAxis]} = {MinV}..{MaxV}, block type {BlockType}";
    }

    // Gives the two axes of a plane, in the order X, Y, Z.
    private static (int U, int V) PlaneAxes(int axis) => axis switch
    {
        0 => (1, 2),
        1 => (0, 2),
        _ => (0, 1),
    };

    // Gives the axis of a vector that is parallel to one axis, or -1 for a different vector.
    private static int OnlyAxis(Vector3 vector)
    {
        int axis = -1;
        for (int i = 0; i < 3; i++)
        {
            if (Component(vector, i) != 0f)
            {
                if (axis >= 0)
                {
                    return -1;
                }

                axis = i;
            }
        }

        return axis;
    }

    private static float Component(Vector3 vector, int axis) => axis switch
    {
        0 => vector.X,
        1 => vector.Y,
        _ => vector.Z,
    };

    private static bool IsInteger(float value) => float.IsFinite(value) && MathF.Round(value) == value;
}
