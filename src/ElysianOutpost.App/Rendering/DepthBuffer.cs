// SPDX-License-Identifier: MPL-2.0

using Brutal.VulkanApi;
using Brutal.VulkanApi.Vma;

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// The depth image of the scene pass and its image view. It has the size of the swapchain images.
/// </summary>
/// <remarks>
/// All the frames in flight use the same depth image. The barrier at the start of each frame makes the depth operations of a frame
/// wait for the depth operations of the previous frame. See <see cref="VulkanRenderer"/>.
/// </remarks>
internal sealed unsafe class DepthBuffer : IDisposable
{
    // The formats without a stencil component. Thus the image has only the depth aspect.
    // Vulkan requires the depth attachment support for at least one of these two formats.
    private static readonly VkFormat[] _candidates = [VkFormat.D32SFloat, VkFormat.X8D24UNormPack32];

    private readonly GpuContext _gpu;
    private readonly VmaAllocation _allocation;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DepthBuffer"/> class.
    /// </summary>
    /// <param name="gpu">The Vulkan objects of the window.</param>
    /// <param name="format">The depth format, from <see cref="SelectFormat"/>.</param>
    /// <param name="extent">The size of the swapchain images, in pixels.</param>
    public DepthBuffer(GpuContext gpu, VkFormat format, VkExtent2D extent)
    {
        _gpu = gpu;
        Format = format;
        Extent = extent;
        var imageInfo = new VkImageCreateInfo
        {
            ImageType = VkImageType._2D,
            Format = format,
            Extent = new VkExtent3D { Width = extent.Width, Height = extent.Height, Depth = 1 },
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = VkSampleCountFlags._1Bit,
            Tiling = VkImageTiling.Optimal,
            Usage = VkImageUsageFlags.DepthStencilAttachmentBit,
            SharingMode = VkSharingMode.Exclusive,
            InitialLayout = VkImageLayout.Undefined,
        };

        // VMA recommends a separate memory block for a large attachment image.
        var allocationInfo = new VmaAllocationCreateInfo
        {
            Usage = VmaMemoryUsage.Auto,
            Flags = VmaAllocationCreateFlags.DedicatedMemoryBit,
        };
        VkResult result = gpu.Allocator.CreateImage(imageInfo, allocationInfo, out VkImage image, out _allocation, out _);
        if (result != VkResult.Success)
        {
            throw new InvalidOperationException($"VMA cannot create the depth image of {extent.Width}x{extent.Height} pixels: {result}.");
        }

        Image = image;
        try
        {
            var viewInfo = new VkImageViewCreateInfo
            {
                Image = image,
                ViewType = VkImageViewType._2D,
                Format = format,
                SubresourceRange = SubresourceRange,
            };
            View = gpu.Device.CreateImageView(in viewInfo, null);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the subresource range of the depth image: the depth aspect, one mip level and one layer.
    /// </summary>
    public static VkImageSubresourceRange SubresourceRange { get; } = new()
    {
        AspectMask = VkImageAspectFlags.DepthBit,
        LevelCount = 1,
        LayerCount = 1,
    };

    /// <summary>
    /// Gets the depth image.
    /// </summary>
    public VkImage Image { get; }

    /// <summary>
    /// Gets the image view of the depth image.
    /// </summary>
    public VkImageView View { get; }

    /// <summary>
    /// Gets the format of the depth image.
    /// </summary>
    public VkFormat Format { get; }

    /// <summary>
    /// Gets the size of the depth image, in pixels.
    /// </summary>
    public VkExtent2D Extent { get; }

    /// <summary>
    /// Selects the first depth format that the GPU supports as a depth attachment with optimal tiling.
    /// </summary>
    /// <param name="device">The physical device.</param>
    /// <returns>The depth format.</returns>
    /// <exception cref="InvalidOperationException">The GPU supports none of the formats. The Vulkan specification does not allow this case.</exception>
    public static VkFormat SelectFormat(PhysicalDevice device)
    {
        foreach (VkFormat format in _candidates)
        {
            VkFormatProperties properties = device.GetFormatProperties(format);
            if (properties.OptimalTilingFeatures.HasFlag(VkFormatFeatureFlags.DepthStencilAttachmentBit))
            {
                return format;
            }
        }

        throw new InvalidOperationException("The GPU supports no depth format as a depth attachment.");
    }

    /// <summary>
    /// Releases the image view, the image and its memory. The GPU must not use them.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!View.IsNull())
        {
            _gpu.Device.DestroyImageView(View, null);
        }

        if (!Image.IsNull())
        {
            _gpu.Allocator.DestroyImage(Image, _allocation);
        }
    }
}
