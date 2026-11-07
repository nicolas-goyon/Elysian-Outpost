// SPDX-License-Identifier: MPL-2.0
//
// The vertex shader of the chunk meshes (#39).
// The vertex layout is ChunkVertex (#51): 8 bytes for each vertex.
// The first value has the position in the chunk (X, Y and Z on 8 bits each) and the index of the normal (3 bits).
// The second value is the block type. The color of the block type comes from the palette.
#version 450

layout(push_constant) uniform PushConstants
{
    // The view matrix multiplied by the projection matrix (FreeFlyCamera and CameraLens).
    // The C# matrices use the row-vector convention. GLSL reads the same memory as the transposed matrix, thus M * p is correct.
    mat4 viewProjection;

    // The world position of the first voxel of the chunk. The shader does not use the W value.
    vec4 chunkPosition;
} pushConstants;

// The 64 colors of the palette (BlockPalette), as specialization constants. Each value is an sRGB color with alpha:
// red in bits 0 to 7, green in bits 8 to 15, blue in bits 16 to 23 and alpha in bits 24 to 31.
layout(constant_id = 0) const uint palette0 = 0u;
layout(constant_id = 1) const uint palette1 = 0u;
layout(constant_id = 2) const uint palette2 = 0u;
layout(constant_id = 3) const uint palette3 = 0u;
layout(constant_id = 4) const uint palette4 = 0u;
layout(constant_id = 5) const uint palette5 = 0u;
layout(constant_id = 6) const uint palette6 = 0u;
layout(constant_id = 7) const uint palette7 = 0u;
layout(constant_id = 8) const uint palette8 = 0u;
layout(constant_id = 9) const uint palette9 = 0u;
layout(constant_id = 10) const uint palette10 = 0u;
layout(constant_id = 11) const uint palette11 = 0u;
layout(constant_id = 12) const uint palette12 = 0u;
layout(constant_id = 13) const uint palette13 = 0u;
layout(constant_id = 14) const uint palette14 = 0u;
layout(constant_id = 15) const uint palette15 = 0u;
layout(constant_id = 16) const uint palette16 = 0u;
layout(constant_id = 17) const uint palette17 = 0u;
layout(constant_id = 18) const uint palette18 = 0u;
layout(constant_id = 19) const uint palette19 = 0u;
layout(constant_id = 20) const uint palette20 = 0u;
layout(constant_id = 21) const uint palette21 = 0u;
layout(constant_id = 22) const uint palette22 = 0u;
layout(constant_id = 23) const uint palette23 = 0u;
layout(constant_id = 24) const uint palette24 = 0u;
layout(constant_id = 25) const uint palette25 = 0u;
layout(constant_id = 26) const uint palette26 = 0u;
layout(constant_id = 27) const uint palette27 = 0u;
layout(constant_id = 28) const uint palette28 = 0u;
layout(constant_id = 29) const uint palette29 = 0u;
layout(constant_id = 30) const uint palette30 = 0u;
layout(constant_id = 31) const uint palette31 = 0u;
layout(constant_id = 32) const uint palette32 = 0u;
layout(constant_id = 33) const uint palette33 = 0u;
layout(constant_id = 34) const uint palette34 = 0u;
layout(constant_id = 35) const uint palette35 = 0u;
layout(constant_id = 36) const uint palette36 = 0u;
layout(constant_id = 37) const uint palette37 = 0u;
layout(constant_id = 38) const uint palette38 = 0u;
layout(constant_id = 39) const uint palette39 = 0u;
layout(constant_id = 40) const uint palette40 = 0u;
layout(constant_id = 41) const uint palette41 = 0u;
layout(constant_id = 42) const uint palette42 = 0u;
layout(constant_id = 43) const uint palette43 = 0u;
layout(constant_id = 44) const uint palette44 = 0u;
layout(constant_id = 45) const uint palette45 = 0u;
layout(constant_id = 46) const uint palette46 = 0u;
layout(constant_id = 47) const uint palette47 = 0u;
layout(constant_id = 48) const uint palette48 = 0u;
layout(constant_id = 49) const uint palette49 = 0u;
layout(constant_id = 50) const uint palette50 = 0u;
layout(constant_id = 51) const uint palette51 = 0u;
layout(constant_id = 52) const uint palette52 = 0u;
layout(constant_id = 53) const uint palette53 = 0u;
layout(constant_id = 54) const uint palette54 = 0u;
layout(constant_id = 55) const uint palette55 = 0u;
layout(constant_id = 56) const uint palette56 = 0u;
layout(constant_id = 57) const uint palette57 = 0u;
layout(constant_id = 58) const uint palette58 = 0u;
layout(constant_id = 59) const uint palette59 = 0u;
layout(constant_id = 60) const uint palette60 = 0u;
layout(constant_id = 61) const uint palette61 = 0u;
layout(constant_id = 62) const uint palette62 = 0u;
layout(constant_id = 63) const uint palette63 = 0u;

// The normals in the order of their index (ChunkVertex.Normals): -X, +X, -Y, +Y, -Z, +Z.
const vec3 normals[6] = vec3[](
    vec3(-1.0, 0.0, 0.0), vec3(1.0, 0.0, 0.0),
    vec3(0.0, -1.0, 0.0), vec3(0.0, 1.0, 0.0),
    vec3(0.0, 0.0, -1.0), vec3(0.0, 0.0, 1.0));

layout(location = 0) in uvec2 inVertex;

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
    // GLSL does not accept a global constant array of specialization constants. Thus the array is local.
    uint palette[64] = uint[](
        palette0, palette1, palette2, palette3, palette4, palette5, palette6, palette7,
        palette8, palette9, palette10, palette11, palette12, palette13, palette14, palette15,
        palette16, palette17, palette18, palette19, palette20, palette21, palette22, palette23,
        palette24, palette25, palette26, palette27, palette28, palette29, palette30, palette31,
        palette32, palette33, palette34, palette35, palette36, palette37, palette38, palette39,
        palette40, palette41, palette42, palette43, palette44, palette45, palette46, palette47,
        palette48, palette49, palette50, palette51, palette52, palette53, palette54, palette55,
        palette56, palette57, palette58, palette59, palette60, palette61, palette62, palette63);

    uint packed = inVertex.x;
    vec3 position = vec3(packed & 0xFFu, (packed >> 8u) & 0xFFu, (packed >> 16u) & 0xFFu);
    gl_Position = pushConstants.viewProjection * vec4(position + pushConstants.chunkPosition.xyz, 1.0);
    outNormal = normals[(packed >> 24u) & 0x7u];
    outColor = srgbToLinear(unpackUnorm4x8(palette[inVertex.y & 63u]).rgb);
}
