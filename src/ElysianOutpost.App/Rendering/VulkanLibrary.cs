// SPDX-License-Identifier: MPL-2.0

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// Helps the BRUTAL bindings to find the Vulkan loader on Linux.
/// </summary>
/// <remarks>
/// On Linux, Brutal.Vulkan 2026.10.2 loads the library "vulkan", thus the file <c>libvulkan.so</c>.
/// Usually only the development package installs this file. A normal installation has only <c>libvulkan.so.1</c>.
/// </remarks>
internal static class VulkanLibrary
{
    private const string BrutalLibraryName = "vulkan";
    private const string LinuxLoaderFile = "libvulkan.so.1";

    private static int _registered;

    /// <summary>
    /// Adds a fallback that loads <c>libvulkan.so.1</c> if the normal search for the library "vulkan" fails.
    /// Call this method before the first use of the Vulkan bindings. A second call has no effect.
    /// </summary>
    public static void AddLinuxFallback()
    {
        if (!OperatingSystem.IsLinux() || Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        AssemblyLoadContext.Default.ResolvingUnmanagedDll += Resolve;
    }

    private static nint Resolve(Assembly assembly, string libraryName) =>
        libraryName == BrutalLibraryName && NativeLibrary.TryLoad(LinuxLoaderFile, out nint handle) ? handle : 0;
}
