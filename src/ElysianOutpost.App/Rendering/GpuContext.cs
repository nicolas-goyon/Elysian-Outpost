// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;
using Brutal.GlfwApi;
using Brutal.Logging;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// The Vulkan objects that do not change with the window size. They are the instance, the surface of the window,
/// the physical device, the logical device and the queue.
/// </summary>
internal sealed unsafe class GpuContext : IDisposable
{
    private const string ValidationLayer = "VK_LAYER_KHRONOS_validation";

    private readonly Instance _instance;
    private readonly VkDebugUtilsMessengerEXT _messenger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuContext"/> class. It creates all the Vulkan objects for the window.
    /// </summary>
    /// <param name="window">The window. It must not have an OpenGL context.</param>
    /// <param name="log">The log category of the renderer.</param>
    /// <exception cref="InvalidOperationException">Vulkan or a suitable GPU is not available.</exception>
    public GpuContext(GlfwWindow window, LogCategory log)
    {
        if (!Glfw.VulkanSupported())
        {
            throw new InvalidOperationException("GLFW cannot find a Vulkan driver. The app needs Vulkan 1.3.");
        }

        VulkanLibrary.AddLinuxFallback();

        _instance = CreateInstance(log, out bool validation);
        try
        {
            if (validation)
            {
                VkDebugUtilsMessengerCreateInfoEXT messengerInfo = ValidationLog.CreateMessengerInfo();
                _messenger = _instance.CreateDebugUtilsMessengerEXT(in messengerInfo, null);
            }

            Surface = CreateSurface(_instance, window);
            (PhysicalDevice, QueueFamily) = SelectPhysicalDevice(_instance, Surface);
            LogPhysicalDevice(log, PhysicalDevice);
            Device = CreateDevice(PhysicalDevice, QueueFamily);
            Queue = Device.GetQueue(QueueFamily, 0);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the surface of the window.
    /// </summary>
    public VkSurfaceKHR Surface { get; }

    /// <summary>
    /// Gets the selected physical device.
    /// </summary>
    public PhysicalDevice PhysicalDevice { get; } = null!;

    /// <summary>
    /// Gets the index of the queue family for the graphics commands and the presentation.
    /// </summary>
    public int QueueFamily { get; }

    /// <summary>
    /// Gets the logical device.
    /// </summary>
    public Device Device { get; } = null!;

    /// <summary>
    /// Gets the queue for the graphics commands and the presentation.
    /// </summary>
    public Queue Queue { get; } = null!;

    /// <summary>
    /// Releases the logical device, the surface, the debug messenger and the instance, in this order.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Device?.Dispose();
        if (!Surface.IsNull())
        {
            _instance.DestroySurfaceKHR(Surface, null);
        }

        if (!_messenger.IsNull())
        {
            _instance.DestroyDebugUtilsMessengerEXT(_messenger, null);
        }

        _instance.Dispose();
    }

    private static Instance CreateInstance(LogCategory log, out bool validation)
    {
        // GLFW gives the instance extensions for the surface of its window system.
        var extensions = new HashSet<string>();
        byte** names = Glfw.GetRequiredInstanceExtensions(out uint count);
        for (int i = 0; i < count; i++)
        {
            extensions.Add(Marshal.PtrToStringUTF8((nint)names[i])!);
        }

        var layers = new HashSet<string>();
        validation = false;
#if DEBUG
        // The validation layers find incorrect uses of Vulkan. They make the app slower, thus only Debug builds use them.
        if (VulkanHelpers.Validation.IsSupported)
        {
            validation = true;
            layers.Add(ValidationLayer);
            extensions.Add(VK.EXT_DEBUG_UTILS_EXTENSION_NAME);
            log.Info($"The Vulkan validation layers are on.");
        }
        else
        {
            log.Warning($"The Vulkan validation layers are not installed. The app runs without them.");
        }
#endif

        // This messenger also reports the messages of vkCreateInstance and vkDestroyInstance.
        VkDebugUtilsMessengerCreateInfoEXT messengerInfo = ValidationLog.CreateMessengerInfo();
        fixed (byte* name = "Elysian Outpost"u8)
        {
            return Vulkan.CreateInstance(new Instance.CreateInfo
            {
                AppInfo = new VkApplicationInfo
                {
                    ApplicationName = name,
                    ApplicationVersion = 1,
                    EngineName = name,
                    EngineVersion = 1,
                    ApiVersion = (int)VulkanHelpers.Api.VERSION_1_3,
                },
                EnabledExtensions = extensions,
                EnabledLayers = layers,
                Next = validation ? &messengerInfo : null,
            });
        }
    }

    private static VkSurfaceKHR CreateSurface(Instance instance, GlfwWindow window)
    {
        int result = Glfw.CreateWindowSurface(instance.VkHandle, window, out ulong surface);
        if (result != (int)VkResult.Success)
        {
            throw new InvalidOperationException($"GLFW cannot create the Vulkan surface of the window: {(VkResult)result}.");
        }

        return new VkSurfaceKHR((nint)surface);
    }

    // Selects a discrete GPU if one exists, else an integrated GPU, else another device type, for example a software driver.
    // The device must support Vulkan 1.3, the swapchain extension, and one queue family for the graphics and the presentation.
    private static (PhysicalDevice Device, int QueueFamily) SelectPhysicalDevice(Instance instance, VkSurfaceKHR surface)
    {
        PhysicalDevice? selected = null;
        int selectedFamily = -1;
        int selectedRank = -1;
        foreach (PhysicalDevice candidate in instance.PhysicalDevices)
        {
            VkPhysicalDeviceProperties properties = candidate.GetProperties();
            if ((uint)properties.ApiVersion < (uint)VulkanHelpers.Api.VERSION_1_3
                || !candidate.SupportsExtension(VK.KHR_SWAPCHAIN_EXTENSION_NAME))
            {
                continue;
            }

            int family = FindQueueFamily(candidate, surface);
            int rank = properties.DeviceType switch
            {
                VkPhysicalDeviceType.DiscreteGpu => 3,
                VkPhysicalDeviceType.IntegratedGpu => 2,
                VkPhysicalDeviceType.VirtualGpu => 1,
                _ => 0,
            };
            if (family >= 0 && rank > selectedRank)
            {
                selected = candidate;
                selectedFamily = family;
                selectedRank = rank;
            }
        }

        return selected is null
            ? throw new InvalidOperationException("No GPU supports Vulkan 1.3 and the presentation to the window.")
            : (selected, selectedFamily);
    }

    private static int FindQueueFamily(PhysicalDevice device, VkSurfaceKHR surface)
    {
        int count = device.GetQueueFamilyProperties_Count();
        Span<VkQueueFamilyProperties> families = stackalloc VkQueueFamilyProperties[count];
        device.GetQueueFamilyProperties_Write(families);
        for (int family = 0; family < count; family++)
        {
            if (families[family].QueueFlags.HasFlag(VkQueueFlags.GraphicsBit)
                && device.GetSurfaceSupportKHR(family, surface))
            {
                return family;
            }
        }

        return -1;
    }

    private static void LogPhysicalDevice(LogCategory log, PhysicalDevice device)
    {
        VkPhysicalDeviceProperties properties = device.GetProperties();
        int loaderVersion;
        Vulkan.EnumerateInstanceVersion(&loaderVersion);
        log.Info($"GPU: {VulkanDeviceInfo.GetDeviceName(in properties)} ({properties.DeviceType}). Driver version: {VulkanDeviceInfo.FormatDriverVersion(in properties)}. Vulkan version: {VulkanDeviceInfo.FormatApiVersion(properties.ApiVersion)} (loader {VulkanDeviceInfo.FormatApiVersion(loaderVersion)}).");
    }

    private static Device CreateDevice(PhysicalDevice physicalDevice, int queueFamily)
    {
        float priority = 1f;
        var queueInfo = new VkDeviceQueueCreateInfo
        {
            QueueFamilyIndex = queueFamily,
            QueueCount = 1,
            QueuePriorities = &priority,
        };

        // Dynamic rendering replaces the render passes. Synchronization2 gives the simpler barriers and submits.
        var features13 = new VkPhysicalDeviceVulkan13Features
        {
            DynamicRendering = true,
            Synchronization2 = true,
        };

        var extensions = new HashSet<string> { VK.KHR_SWAPCHAIN_EXTENSION_NAME };
        VulkanHelpers.AddMandatoryIfSupportedDeviceExtensions(extensions, physicalDevice);
        return physicalDevice.CreateDevice(new Device.CreateInfo
        {
            Extensions = extensions,
            QueueCreateInfos = new ReadOnlySpan<VkDeviceQueueCreateInfo>(&queueInfo, 1),
            Next = &features13,
        });
    }
}
