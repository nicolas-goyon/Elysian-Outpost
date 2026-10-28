// SPDX-License-Identifier: MPL-2.0
//
// The fragment shader of the selection box (#40). Black lines: black is the same in sRGB and in linear values.
#version 450

layout(location = 0) out vec4 outColor;

void main()
{
    outColor = vec4(0.0, 0.0, 0.0, 1.0);
}
