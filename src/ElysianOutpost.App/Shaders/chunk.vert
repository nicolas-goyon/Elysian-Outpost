// SPDX-License-Identifier: MPL-2.0
//
// The vertex shader of the chunk meshes (#39).
// The vertex layout is ChunkVertex: the position in the chunk, the flat normal of the quad, and the sRGB color of the block type.
#version 450

layout(push_constant) uniform PushConstants
{
    // The view matrix multiplied by the projection matrix (FreeFlyCamera and CameraLens).
    // The C# matrices use the row-vector convention. GLSL reads the same memory as the transposed matrix, thus M * p is correct.
    mat4 viewProjection;

    // The world position of the first voxel of the chunk. The shader does not use the W value.
    vec4 chunkPosition;
} pushConstants;

layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec3 inNormal;
layout(location = 2) in vec4 inColor;

layout(location = 0) out vec3 outNormal;
layout(location = 1) out vec3 outColor;

// Converts sRGB values to linear values (IEC 61966-2-1). The lighting needs linear values.
vec3 srgbToLinear(vec3 color)
{
    vec3 low = color / 12.92;
    vec3 high = pow((color + 0.055) / 1.055, vec3(2.4));
    return mix(low, high, greaterThan(color, vec3(0.04045)));
}

void main()
{
    gl_Position = pushConstants.viewProjection * vec4(inPosition + pushConstants.chunkPosition.xyz, 1.0);
    outNormal = inNormal;
    outColor = srgbToLinear(inColor.rgb);
}
