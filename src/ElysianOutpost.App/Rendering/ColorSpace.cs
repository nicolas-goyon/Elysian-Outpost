// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using Brutal.VulkanApi;

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// Converts colors from the sRGB color space to linear values.
/// </summary>
internal static class ColorSpace
{
    /// <summary>
    /// Gets a value that tells if a swapchain format has sRGB encoding.
    /// With such a format, the GPU converts the linear output of the shaders to sRGB.
    /// </summary>
    /// <param name="format">The format of the swapchain images.</param>
    /// <returns><c>true</c> for an sRGB format.</returns>
    public static bool IsSrgb(VkFormat format) => format is VkFormat.B8G8R8A8SRGB or VkFormat.R8G8B8A8SRGB;

    /// <summary>
    /// Converts the red, green and blue components of an sRGB color to linear values. The alpha value does not change.
    /// </summary>
    /// <param name="color">The sRGB color, with components from 0 to 1.</param>
    /// <returns>The color with linear values.</returns>
    public static float4 SrgbToLinear(float4 color) =>
        new(SrgbToLinear(color.X), SrgbToLinear(color.Y), SrgbToLinear(color.Z), color.W);

    /// <summary>
    /// Converts one sRGB color component to a linear value (IEC 61966-2-1).
    /// </summary>
    /// <param name="value">The sRGB value, from 0 to 1.</param>
    /// <returns>The linear value.</returns>
    public static float SrgbToLinear(float value) =>
        value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
}
