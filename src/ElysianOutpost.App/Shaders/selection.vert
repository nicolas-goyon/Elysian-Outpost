// SPDX-License-Identifier: MPL-2.0
//
// The vertex shader of the selection box (#40): the 12 edges of a cube, as a line list.
// The shader makes the 24 vertices from gl_VertexIndex, thus the draw has no vertex buffer.
#version 450

layout(push_constant) uniform PushConstants
{
    // The view matrix multiplied by the projection matrix, as in chunk.vert.
    mat4 viewProjection;

    // XYZ: the world position of the lowest corner of the box. W: the length of the edges.
    vec4 box;
} pushConstants;

// The corners of the cube: bit 0 is X, bit 1 is Y and bit 2 is Z. Each edge connects two corners that differ in one bit.
const ivec2 edges[12] = ivec2[](
    ivec2(0, 1), ivec2(2, 3), ivec2(4, 5), ivec2(6, 7),
    ivec2(0, 2), ivec2(1, 3), ivec2(4, 6), ivec2(5, 7),
    ivec2(0, 4), ivec2(1, 5), ivec2(2, 6), ivec2(3, 7));

// The lines and the faces of the voxel have almost the same depth. Without an offset, the depth test hides parts of the lines.
// Vulkan applies the depth bias of the pipeline only to polygons. Thus the shader moves the lines nearer to the camera.
// The depth is about 1 - near / distance (CameraLens). For one voxel at the distance d, the depth changes by about 0.3 / d².
// The offset is 2% of a voxel at 18 voxels and 30% of a voxel at 64 voxels. Thus the voxel still hides the edges behind it.
const float depthOffset = 2e-5;

void main()
{
    ivec2 edge = edges[gl_VertexIndex / 2];
    int corner = (gl_VertexIndex % 2 == 0) ? edge.x : edge.y;
    vec3 offset = vec3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
    vec4 position = pushConstants.viewProjection * vec4(pushConstants.box.xyz + offset * pushConstants.box.w, 1.0);
    position.z -= depthOffset * position.w;
    gl_Position = position;
}
