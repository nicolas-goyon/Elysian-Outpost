// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using Brutal.VulkanApi;

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// The swapchain of the window, with an image view and a "rendering finished" semaphore for each image.
/// </summary>
/// <remarks>
/// Each image has its own semaphore for the presentation. A semaphore of a frame in flight is not sufficient,
/// because the presentation engine can still use it when the next frame starts.
/// </remarks>
internal sealed unsafe class Swapchain : IDisposable
{
    private readonly GpuContext _gpu;

    /// <summary>
    /// Initializes a new instance of the <see cref="Swapchain"/> class.
    /// </summary>
    /// <param name="gpu">The Vulkan objects of the window.</param>
    /// <param name="framebufferSize">The size of the framebuffer of the window, in pixels. Both values must be more than 0.</param>
    public Swapchain(GpuContext gpu, int2 framebufferSize)
    {
        _gpu = gpu;
        Create(framebufferSize, VkSwapchainKHR.NullHandle);
    }

    /// <summary>
    /// Gets the swapchain handle.
    /// </summary>
    public VkSwapchainKHR Handle { get; private set; }

    /// <summary>
    /// Gets the format of the images.
    /// </summary>
    public VkFormat Format { get; private set; }

    /// <summary>
    /// Gets the size of the images, in pixels.
    /// </summary>
    public VkExtent2D Extent { get; private set; }

    /// <summary>
    /// Gets the images of the swapchain.
    /// </summary>
    public VkImage[] Images { get; private set; } = [];

    /// <summary>
    /// Gets the image views, one for each image.
    /// </summary>
    public VkImageView[] ImageViews { get; private set; } = [];

    /// <summary>
    /// Gets the semaphores that the presentation waits for, one for each image.
    /// </summary>
    public VkSemaphore[] RenderingFinished { get; private set; } = [];

    /// <summary>
    /// Creates the swapchain again, for example after a change of the window size.
    /// </summary>
    /// <param name="framebufferSize">The new size of the framebuffer, in pixels. Both values must be more than 0.</param>
    public void Recreate(int2 framebufferSize)
    {
        // The old images can be in use. Thus the device must be idle before the release of their resources.
        _gpu.Device.WaitIdle();
        VkSwapchainKHR old = Handle;
        DestroyImageResources();
        Create(framebufferSize, old);
        _gpu.Device.DestroySwapchainKHR(old, null);
    }

    /// <summary>
    /// Releases the image views, the semaphores and the swapchain. The device must be idle.
    /// </summary>
    public void Dispose()
    {
        DestroyImageResources();
        if (!Handle.IsNull())
        {
            _gpu.Device.DestroySwapchainKHR(Handle, null);
            Handle = VkSwapchainKHR.NullHandle;
        }
    }

    private void Create(int2 framebufferSize, VkSwapchainKHR oldSwapchain)
    {
        Device device = _gpu.Device;
        PhysicalDevice physicalDevice = _gpu.PhysicalDevice;
        VkSurfaceCapabilitiesKHR capabilities = physicalDevice.GetSurfaceCapabilitiesKHR(_gpu.Surface);
        VkSurfaceFormatKHR surfaceFormat = SelectFormat(physicalDevice, _gpu.Surface);

        // FIFO waits for the vertical blank (vsync). All Vulkan drivers support it.
        var info = new VkSwapchainCreateInfoKHR
        {
            Surface = _gpu.Surface,
            MinImageCount = SelectImageCount(capabilities),
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = SelectExtent(capabilities, framebufferSize),
            ImageArrayLayers = 1,
            ImageUsage = VkImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode = VkSharingMode.Exclusive,
            PreTransform = capabilities.CurrentTransform,
            CompositeAlpha = SelectCompositeAlpha(capabilities),
            PresentMode = VkPresentModeKHR.FifoKHR,
            Clipped = true,
            OldSwapchain = oldSwapchain,
        };
        Handle = device.CreateSwapchainKHR(in info, null);
        Format = info.ImageFormat;
        Extent = info.ImageExtent;

        int count = device.GetSwapchainImagesKHR_Count(Handle);
        Images = new VkImage[count];
        device.GetSwapchainImagesKHR_Write(Handle, Images);

        ImageViews = new VkImageView[count];
        RenderingFinished = new VkSemaphore[count];
        var semaphoreInfo = new VkSemaphoreCreateInfo();
        for (int i = 0; i < count; i++)
        {
            var viewInfo = new VkImageViewCreateInfo
            {
                Image = Images[i],
                ViewType = VkImageViewType._2D,
                Format = Format,
                SubresourceRange = new VkImageSubresourceRange
                {
                    AspectMask = VkImageAspectFlags.ColorBit,
                    LevelCount = 1,
                    LayerCount = 1,
                },
            };
            ImageViews[i] = device.CreateImageView(in viewInfo, null);
            RenderingFinished[i] = device.CreateSemaphore(in semaphoreInfo, null);
        }
    }

    private void DestroyImageResources()
    {
        foreach (VkSemaphore semaphore in RenderingFinished)
        {
            _gpu.Device.DestroySemaphore(semaphore, null);
        }

        foreach (VkImageView view in ImageViews)
        {
            _gpu.Device.DestroyImageView(view, null);
        }

        RenderingFinished = [];
        ImageViews = [];
        Images = [];
    }

    // Selects an sRGB format. Then the GPU converts the linear colors of the shaders to sRGB.
    private static VkSurfaceFormatKHR SelectFormat(PhysicalDevice device, VkSurfaceKHR surface)
    {
        int count = device.GetSurfaceFormatsKHR_Count(surface);
        Span<VkSurfaceFormatKHR> formats = stackalloc VkSurfaceFormatKHR[count];
        device.GetSurfaceFormatsKHR_Write(surface, formats);
        foreach (VkSurfaceFormatKHR format in formats)
        {
            if (format.Format is VkFormat.B8G8R8A8SRGB or VkFormat.R8G8B8A8SRGB
                && format.ColorSpace == VkColorSpaceKHR.SRGBNonlinearKHR)
            {
                return format;
            }
        }

        return formats[0];
    }

    // One image more than the minimum lets the app draw a frame while the GPU presents the other images.
    private static int SelectImageCount(in VkSurfaceCapabilitiesKHR capabilities)
    {
        int count = capabilities.MinImageCount + 1;
        return capabilities.MaxImageCount > 0 ? Math.Min(count, capabilities.MaxImageCount) : count;
    }

    // If the current extent is 0xFFFFFFFF (-1 as an int), the window system lets the swapchain select the size.
    private static VkExtent2D SelectExtent(in VkSurfaceCapabilitiesKHR capabilities, int2 framebufferSize)
    {
        if (capabilities.CurrentExtent.Width != -1)
        {
            return capabilities.CurrentExtent;
        }

        return new VkExtent2D(
            Math.Clamp(framebufferSize.X, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width),
            Math.Clamp(framebufferSize.Y, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height));
    }

    private static VkCompositeAlphaFlagsKHR SelectCompositeAlpha(in VkSurfaceCapabilitiesKHR capabilities)
    {
        ReadOnlySpan<VkCompositeAlphaFlagsKHR> preferred =
        [
            VkCompositeAlphaFlagsKHR.OpaqueBitKHR,
            VkCompositeAlphaFlagsKHR.InheritBitKHR,
            VkCompositeAlphaFlagsKHR.PreMultipliedBitKHR,
            VkCompositeAlphaFlagsKHR.PostMultipliedBitKHR,
        ];
        foreach (VkCompositeAlphaFlagsKHR mode in preferred)
        {
            if (capabilities.SupportedCompositeAlpha.HasFlag(mode))
            {
                return mode;
            }
        }

        return VkCompositeAlphaFlagsKHR.OpaqueBitKHR;
    }
}
