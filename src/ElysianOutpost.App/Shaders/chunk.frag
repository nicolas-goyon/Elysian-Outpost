// SPDX-License-Identifier: MPL-2.0
//
// The fragment shader of the chunk meshes (#39).
// One directional light and one ambient light, then the "Neutral" tonemapping of the legacy post-processing.
// The light values come from SceneLighting, as specialization constants. ChunkPipeline gives them.
#version 450

// The directional light: the direction from the surface to the light, and the color multiplied by the intensity.
layout(constant_id = 0) const float directionToLightX = 0.0;
layout(constant_id = 1) const float directionToLightY = 1.0;
layout(constant_id = 2) const float directionToLightZ = 0.0;
layout(constant_id = 3) const float lightColorR = 1.0;
layout(constant_id = 4) const float lightColorG = 1.0;
layout(constant_id = 5) const float lightColorB = 1.0;

// The ambient light: the coefficients k0 to k8 of AmbientProbe (L2 spherical harmonics), red, green and blue for each coefficient.
layout(constant_id = 6) const float k0R = 0.0;
layout(constant_id = 7) const float k0G = 0.0;
layout(constant_id = 8) const float k0B = 0.0;
layout(constant_id = 9) const float k1R = 0.0;
layout(constant_id = 10) const float k1G = 0.0;
layout(constant_id = 11) const float k1B = 0.0;
layout(constant_id = 12) const float k2R = 0.0;
layout(constant_id = 13) const float k2G = 0.0;
layout(constant_id = 14) const float k2B = 0.0;
layout(constant_id = 15) const float k3R = 0.0;
layout(constant_id = 16) const float k3G = 0.0;
layout(constant_id = 17) const float k3B = 0.0;
layout(constant_id = 18) const float k4R = 0.0;
layout(constant_id = 19) const float k4G = 0.0;
layout(constant_id = 20) const float k4B = 0.0;
layout(constant_id = 21) const float k5R = 0.0;
layout(constant_id = 22) const float k5G = 0.0;
layout(constant_id = 23) const float k5B = 0.0;
layout(constant_id = 24) const float k6R = 0.0;
layout(constant_id = 25) const float k6G = 0.0;
layout(constant_id = 26) const float k6B = 0.0;
layout(constant_id = 27) const float k7R = 0.0;
layout(constant_id = 28) const float k7G = 0.0;
layout(constant_id = 29) const float k7B = 0.0;
layout(constant_id = 30) const float k8R = 0.0;
layout(constant_id = 31) const float k8G = 0.0;
layout(constant_id = 32) const float k8B = 0.0;

// True if the swapchain format is not sRGB (a UNORM format). Then the GPU does not convert the output to sRGB, and the shader must do it.
layout(constant_id = 33) const bool encodeSrgb = false;

layout(location = 0) in vec3 inNormal;
layout(location = 1) in vec3 inColor;

layout(location = 0) out vec4 outColor;

// The same formula as AmbientProbe.Evaluate. A negative value changes to zero, as in SampleSH of Unity.
vec3 ambientLight(vec3 n)
{
    vec3 value = vec3(k0R, k0G, k0B)
        + vec3(k1R, k1G, k1B) * n.y
        + vec3(k2R, k2G, k2B) * n.z
        + vec3(k3R, k3G, k3B) * n.x
        + vec3(k4R, k4G, k4B) * (n.x * n.y)
        + vec3(k5R, k5G, k5B) * (n.y * n.z)
        + vec3(k6R, k6G, k6B) * (3.0 * n.z * n.z - 1.0)
        + vec3(k7R, k7G, k7B) * (n.x * n.z)
        + vec3(k8R, k8G, k8B) * (n.x * n.x - n.y * n.y);
    return max(value, vec3(0.0));
}

// The "Neutral" tonemapping of URP (NeutralTonemap in Color.hlsl). The legacy scene uses it in its post-processing volume.
vec3 neutralCurve(vec3 x, float a, float b, float c, float d, float e, float f)
{
    return ((x * (a * x + c * b) + d * e) / (x * (a * x + b) + d * f)) - e / f;
}

vec3 neutralTonemap(vec3 x)
{
    const float a = 0.2;
    const float b = 0.29;
    const float c = 0.24;
    const float d = 0.272;
    const float e = 0.02;
    const float f = 0.3;
    const float whiteLevel = 5.3;
    const float whiteClip = 1.0;

    vec3 whiteScale = vec3(1.0) / neutralCurve(vec3(whiteLevel), a, b, c, d, e, f);
    x = neutralCurve(x * whiteScale, a, b, c, d, e, f);
    x *= whiteScale;
    return x / whiteClip;
}

// Converts linear values to sRGB values (IEC 61966-2-1).
vec3 linearToSrgb(vec3 color)
{
    color = clamp(color, 0.0, 1.0);
    vec3 low = color * 12.92;
    vec3 high = 1.055 * pow(color, vec3(1.0 / 2.4)) - 0.055;
    return mix(low, high, greaterThan(color, vec3(0.0031308)));
}

void main()
{
    vec3 normal = normalize(inNormal);
    vec3 directionToLight = vec3(directionToLightX, directionToLightY, directionToLightZ);
    vec3 lightColor = vec3(lightColorR, lightColorG, lightColorB);
    vec3 diffuse = lightColor * max(dot(normal, directionToLight), 0.0);
    vec3 color = neutralTonemap(inColor * (ambientLight(normal) + diffuse));

    // With an sRGB swapchain format, the GPU converts this linear value to sRGB.
    outColor = vec4(encodeSrgb ? linearToSrgb(color) : color, 1.0);
}
