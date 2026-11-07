// SPDX-License-Identifier: MPL-2.0

using System.Reflection;
using Brutal.GlfwApi;
using Brutal.Numerics;
using ElysianOutpost.App.Rendering;
using ElysianOutpost.Core.Diagnostics;

namespace ElysianOutpost.App;

/// <summary>
/// Makes the hardware report of this computer (#81): the system values, the GPU of the renderer, the monitors and the window.
/// </summary>
/// <remarks>
/// Call <see cref="Create"/> on the main thread, because GLFW gives the monitors and the window values only on the main thread.
/// </remarks>
internal static class HardwareReports
{
    /// <summary>
    /// Makes the hardware report.
    /// </summary>
    /// <param name="window">The window of the app.</param>
    /// <param name="renderer">The renderer. It gives the GPU values and the presentation mode.</param>
    /// <param name="system">The system values. <see cref="SystemInfo.Collect"/> gives them. The available RAM changes, thus collect them again for each report.</param>
    /// <returns>The report.</returns>
    public static HardwareReport Create(GlfwWindow window, VulkanRenderer renderer, SystemInfo system)
    {
        // GLFW gives the primary monitor first.
        ReadOnlySpan<GlfwMonitor> monitors = Glfw.GetMonitors();
        var monitorInfo = new List<MonitorInfo>(monitors.Length);
        for (int i = 0; i < monitors.Length; i++)
        {
            GlfwVidMode mode = monitors[i].VideoMode;
            float2 scale = monitors[i].ContentScale;
            monitorInfo.Add(new MonitorInfo(i == 0, mode.Width, mode.Height, mode.RefreshRate, scale.X));
        }

        int2 framebuffer = window.FramebufferSize;
        return new HardwareReport
        {
            AppVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown",
            System = system,
            Gpu = renderer.GpuInfo,
            Monitors = monitorInfo,
            Window = new WindowInfo(window.Width, window.Height, framebuffer.X, framebuffer.Y, VulkanRenderer.PresentMode, VerticalSync: true, Glfw.GetPlatform().ToString()),
        };
    }
}
