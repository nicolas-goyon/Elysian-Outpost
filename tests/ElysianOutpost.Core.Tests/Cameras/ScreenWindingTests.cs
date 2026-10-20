// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Cameras;
using ElysianOutpost.Core.Meshing;
using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.Tests.Cameras;

/// <summary>
/// Projects the quads of one voxel with the camera matrices, and examines the winding order of the triangles on the screen.
/// </summary>
/// <remarks>
/// <para>
/// The legacy project draws each quad as the triangles (0, 1, 2) and (2, 3, 0) (<c>ObjExporter.ToUnityMesh</c>).
/// In Unity, a front face is clockwise on the screen. With the camera matrices, the image on the screen is not mirrored.
/// Thus the faces that point to the camera stay clockwise on the screen, and their signed area of Vulkan is negative.
/// </para>
/// <para>
/// The chunk renderer (#39) must thus use <c>VK_FRONT_FACE_CLOCKWISE</c> with these matrices and this triangle order.
/// The reason is the winding convention of Unity. The Y axis of the clip space does not change the winding order on the screen.
/// </para>
/// </remarks>
public sealed class ScreenWindingTests
{
    private const float Width = 1280f;
    private const float Height = 720f;
    private const int VoxelCoordinate = 1;

    private static readonly FreeFlyCameraSettings _settings = new();

    [Theory]
    [InlineData(0f, 0f, true)]
    [InlineData(180f, 0f, true)]
    [InlineData(90f, 0f, true)]
    [InlineData(270f, 0f, true)]
    [InlineData(0f, 80f, true)]
    [InlineData(0f, -80f, true)]
    [InlineData(45f, 30f, true)]
    [InlineData(225f, -30f, true)]
    [InlineData(0f, 0f, false)]
    [InlineData(180f, 0f, false)]
    [InlineData(90f, 0f, false)]
    [InlineData(270f, 0f, false)]
    [InlineData(0f, 80f, false)]
    [InlineData(0f, -80f, false)]
    [InlineData(45f, 30f, false)]
    [InlineData(225f, -30f, false)]
    public void MesherQuads_FacesThatPointToTheCamera_AreClockwiseOnTheScreen(float yaw, float pitch, bool optimizedMesher)
    {
        var center = new float3(VoxelCoordinate + 0.5f);
        float3 forward = new FreeFlyCamera(_settings, float3.Zero, yaw, pitch).Forward;
        var camera = new FreeFlyCamera(_settings, center - (forward * 10f), yaw, pitch);
        float4x4 viewProjection = camera.CreateViewMatrix() * new CameraLens().CreateProjectionMatrix(Width / Height);

        List<MeshQuad> quads = OneVoxelQuads(optimizedMesher);
        Assert.Equal(6, quads.Count);

        int facesToTheCamera = 0;
        foreach (MeshQuad quad in quads)
        {
            float3[] vertices = [ToFloat3(quad.Vertex0), ToFloat3(quad.Vertex1), ToFloat3(quad.Vertex2), ToFloat3(quad.Vertex3)];
            float3 normal = OutwardNormal(vertices);
            float3 faceCenter = (vertices[0] + vertices[1] + vertices[2] + vertices[3]) * 0.25f;
            bool toTheCamera = float3.Dot(camera.Position - faceCenter, normal) > 0f;
            facesToTheCamera += toTheCamera ? 1 : 0;

            foreach ((int a, int b, int c) in new[] { (0, 1, 2), (2, 3, 0) })
            {
                float area = VulkanSignedArea(viewProjection, vertices[a], vertices[b], vertices[c]);
                Assert.True(
                    toTheCamera ? area < 0f : area > 0f,
                    $"Face {normal}, triangle ({a}, {b}, {c}): area {area}. The face {(toTheCamera ? "points" : "does not point")} to the camera.");
            }
        }

        Assert.InRange(facesToTheCamera, 1, 3);
    }

    private static List<MeshQuad> OneVoxelQuads(bool optimizedMesher)
    {
        var voxels = new ushort[3, 3, 3];
        voxels[VoxelCoordinate, VoxelCoordinate, VoxelCoordinate] = 22;
        var chunk = new Chunk(voxels, new int3(0, 0, 0));
        return optimizedMesher ? new DisjointSetMeshOptimizer(new Mesh()).Optimize(chunk).Quads : chunk.ToMesh().Quads;
    }

    // The quad normal of the optimized mesher is not correct (#46). Thus the test finds the face from the vertices:
    // the four vertices have the same coordinate on the axis of the normal.
    private static float3 OutwardNormal(float3[] vertices)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            float value = vertices[0][axis];
            if (vertices.All(vertex => vertex[axis] == value))
            {
                var normal = float3.Zero;
                normal[axis] = value == VoxelCoordinate ? -1f : 1f;
                return normal;
            }
        }

        throw new InvalidOperationException("The quad is not aligned with an axis.");
    }

    // The signed area of the Vulkan specification, in framebuffer coordinates (Y down):
    // a = -1/2 × sum(x(i) × y(i+1) - x(i+1) × y(i)). VK_FRONT_FACE_CLOCKWISE makes a negative area a front face.
    private static float VulkanSignedArea(float4x4 viewProjection, float3 a, float3 b, float3 c)
    {
        float2[] points = [ToFramebuffer(viewProjection, a), ToFramebuffer(viewProjection, b), ToFramebuffer(viewProjection, c)];
        float sum = 0f;
        for (int i = 0; i < 3; i++)
        {
            float2 current = points[i];
            float2 next = points[(i + 1) % 3];
            sum += (current.X * next.Y) - (next.X * current.Y);
        }

        return -0.5f * sum;
    }

    private static float2 ToFramebuffer(float4x4 viewProjection, float3 point)
    {
        float4 clip = float4.Transform(point, viewProjection);
        Assert.True(clip.W > 0f, "The point is behind the camera.");
        return new float2(((clip.X / clip.W) + 1f) * Width / 2f, ((clip.Y / clip.W) + 1f) * Height / 2f);
    }

    private static float3 ToFloat3(System.Numerics.Vector3 vector) => new(vector.X, vector.Y, vector.Z);
}
