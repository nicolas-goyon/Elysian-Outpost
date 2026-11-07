// SPDX-License-Identifier: MPL-2.0

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElysianOutpost.Core.Diagnostics;

/// <summary>
/// The hardware and the monitors of the computer that made a performance profile (#81).
/// </summary>
/// <remarks>
/// <para>
/// The report contains no personal data: no user name, no computer name, no serial number and no network address.
/// The monitors have no name, because a monitor name can contain a serial number.
/// </para>
/// <para>
/// The app keeps the report on the computer. It does not send it. #84 adds the consent to share data.
/// </para>
/// </remarks>
public sealed record HardwareReport
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // The file is not HTML. Thus a character such as "+" in the app version stays readable.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Gets the version of the format of the report. A change of the properties increments it.</summary>
    public int FormatVersion { get; init; } = 1;

    /// <summary>Gets the version of the app.</summary>
    public required string AppVersion { get; init; }

    /// <summary>Gets the operating system, the CPU and the RAM.</summary>
    public required SystemInfo System { get; init; }

    /// <summary>Gets the GPU of the renderer.</summary>
    public required GpuInfo Gpu { get; init; }

    /// <summary>Gets the monitors, the primary monitor first.</summary>
    public required IReadOnlyList<MonitorInfo> Monitors { get; init; }

    /// <summary>Gets the window and the presentation.</summary>
    public required WindowInfo Window { get; init; }

    /// <summary>
    /// Writes the report as indented JSON text, with camel-case property names.
    /// </summary>
    /// <returns>The JSON text.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, _jsonOptions);
}

/// <summary>
/// The GPU of the renderer, from Vulkan.
/// </summary>
/// <param name="Name">The name of the GPU.</param>
/// <param name="Vendor">The vendor, from the PCI vendor ID, for example <c>NVIDIA</c>, or the ID in hexadecimal for an unknown vendor.</param>
/// <param name="Type">The type of the GPU, for example <c>IntegratedGpu</c>, <c>DiscreteGpu</c> or <c>Cpu</c>.</param>
/// <param name="DriverVersion">The version of the driver.</param>
/// <param name="VulkanVersion">The Vulkan version of the GPU.</param>
/// <param name="DeviceLocalMemoryBytes">The total size of the memory heaps of the GPU (device local), in bytes.</param>
public sealed record GpuInfo(string Name, string Vendor, string Type, string DriverVersion, string VulkanVersion, long DeviceLocalMemoryBytes)
{
    /// <summary>
    /// Gets the name of a GPU vendor from its PCI vendor ID.
    /// </summary>
    /// <param name="vendorId">The PCI vendor ID of Vulkan.</param>
    /// <returns>The name, or the ID in hexadecimal, for example <c>0x1234</c>.</returns>
    public static string VendorName(int vendorId) => vendorId switch
    {
        0x1002 => "AMD",
        0x10DE => "NVIDIA",
        0x8086 => "Intel",
        0x13B5 => "ARM",
        0x5143 => "Qualcomm",
        0x106B => "Apple",
        0x10005 => "Mesa",
        _ => $"0x{vendorId:X4}",
    };
}

/// <summary>
/// One monitor, from GLFW.
/// </summary>
/// <param name="Primary"><c>true</c> for the primary monitor.</param>
/// <param name="Width">The width of the current video mode, in pixels.</param>
/// <param name="Height">The height of the current video mode, in pixels.</param>
/// <param name="RefreshRate">The refresh rate of the current video mode, in hertz.</param>
/// <param name="ContentScale">The content scale of the monitor, for example 1.5 for a scale of 150 %.</param>
public sealed record MonitorInfo(bool Primary, int Width, int Height, int RefreshRate, float ContentScale);

/// <summary>
/// The window of the app and the presentation mode.
/// </summary>
/// <param name="Width">The width of the window, in screen coordinates.</param>
/// <param name="Height">The height of the window, in screen coordinates.</param>
/// <param name="FramebufferWidth">The width of the framebuffer, in pixels.</param>
/// <param name="FramebufferHeight">The height of the framebuffer, in pixels.</param>
/// <param name="PresentMode">The presentation mode of the swapchain, for example <c>FIFO</c>.</param>
/// <param name="VerticalSync"><c>true</c> if the presentation waits for the vertical synchronization.</param>
/// <param name="Platform">The window platform of GLFW, for example <c>X11</c>, <c>Wayland</c> or <c>Win32</c>.</param>
public sealed record WindowInfo(int Width, int Height, int FramebufferWidth, int FramebufferHeight, string PresentMode, bool VerticalSync, string Platform);
