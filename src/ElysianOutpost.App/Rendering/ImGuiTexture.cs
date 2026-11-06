// SPDX-License-Identifier: MPL-2.0

using Brutal;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// An image that ImGui can draw, for example the BRUTAL logo of the start screen.
/// </summary>
/// <remarks>
/// <para>
/// The constructor copies the pixels to a GPU image through a staging buffer, and waits until the GPU completes the copy.
/// Then the Vulkan backend of ImGui gets a descriptor set for the image (<see cref="ImGuiPass.AddTexture"/>).
/// </para>
/// <para>
/// The pixels are sRGB values. For an sRGB swapchain, the image has an sRGB format. Then the GPU converts the pixels to linear values
/// when the ImGui shader reads them, and the swapchain converts the output back to sRGB.
/// For a UNORM swapchain, the image has a UNORM format, and the values go to the screen without a conversion.
/// </para>
/// </remarks>
internal sealed unsafe class ImGuiTexture : IDisposable
{
    private const int BytesPerPixel = 4;

    private static readonly VkImageSubresourceRange _colorRange = new()
    {
        AspectMask = VkImageAspectFlags.ColorBit,
        LevelCount = 1,
        LayerCount = 1,
    };

    private readonly GpuContext _gpu;
    private readonly ImGuiPass _pass;
    private readonly VkImage _image;
    private readonly VmaAllocation _allocation;
    private readonly VkImageView _view;
    private readonly VkSampler _sampler;
    private readonly bool _registered;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImGuiTexture"/> class and copies the pixels to the GPU.
    /// </summary>
    /// <param name="gpu">The Vulkan objects of the window.</param>
    /// <param name="pass">The ImGui pass. Its backend makes the descriptor set of the texture.</param>
    /// <param name="rgbaPixels">The pixels, 4 bytes for each pixel (red, green, blue and alpha), in rows from the top row.</param>
    /// <param name="width">The width of the image, in pixels.</param>
    /// <param name="height">The height of the image, in pixels.</param>
    /// <exception cref="ArgumentException">The number of bytes is not <paramref name="width"/> × <paramref name="height"/> × 4.</exception>
    public ImGuiTexture(GpuContext gpu, ImGuiPass pass, ReadOnlySpan<byte> rgbaPixels, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (rgbaPixels.Length != (long)width * height * BytesPerPixel)
        {
            throw new ArgumentException($"An image of {width}x{height} pixels needs {(long)width * height * BytesPerPixel} bytes, not {rgbaPixels.Length}.", nameof(rgbaPixels));
        }

        _gpu = gpu;
        _pass = pass;
        Size = new float2(width, height);
        VkFormat format = ColorSpace.IsSrgb(pass.Format) ? VkFormat.R8G8B8A8SRGB : VkFormat.R8G8B8A8UNorm;
        var imageInfo = new VkImageCreateInfo
        {
            ImageType = VkImageType._2D,
            Format = format,
            Extent = new VkExtent3D { Width = width, Height = height, Depth = 1 },
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = VkSampleCountFlags._1Bit,
            Tiling = VkImageTiling.Optimal,
            Usage = VkImageUsageFlags.SampledBit | VkImageUsageFlags.TransferDstBit,
            SharingMode = VkSharingMode.Exclusive,
            InitialLayout = VkImageLayout.Undefined,
        };
        var allocationInfo = new VmaAllocationCreateInfo { Usage = VmaMemoryUsage.Auto };
        VkResult result = gpu.Allocator.CreateImage(imageInfo, allocationInfo, out _image, out _allocation, out _);
        if (result != VkResult.Success)
        {
            throw new InvalidOperationException($"VMA cannot create an image of {width}x{height} pixels for ImGui: {result}.");
        }

        try
        {
            Upload(rgbaPixels, width, height);
            var viewInfo = new VkImageViewCreateInfo
            {
                Image = _image,
                ViewType = VkImageViewType._2D,
                Format = format,
                SubresourceRange = _colorRange,
            };
            _view = gpu.Device.CreateImageView(in viewInfo, null);

            // Linear filters for a smooth logo at all scales. The image has no mipmaps.
            var samplerInfo = new VkSamplerCreateInfo
            {
                MagFilter = VkFilter.Linear,
                MinFilter = VkFilter.Linear,
                AddressModeU = VkSamplerAddressMode.ClampToEdge,
                AddressModeV = VkSamplerAddressMode.ClampToEdge,
                AddressModeW = VkSamplerAddressMode.ClampToEdge,
                MaxLod = 0,
            };
            _sampler = gpu.Device.CreateSampler(in samplerInfo, null);
            Reference = pass.AddTexture(_sampler, _view);
            _registered = true;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the reference of the texture for the ImGui draw functions.
    /// </summary>
    public ImTextureRef Reference { get; }

    /// <summary>
    /// Gets the size of the image, in pixels.
    /// </summary>
    public float2 Size { get; }

    /// <summary>
    /// Releases the descriptor set, the sampler, the view and the image. The GPU must not use them.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_registered)
        {
            _pass.RemoveTexture(Reference);
        }

        if (!_sampler.IsNull())
        {
            _gpu.Device.DestroySampler(_sampler, null);
        }

        if (!_view.IsNull())
        {
            _gpu.Device.DestroyImageView(_view, null);
        }

        if (!_image.IsNull())
        {
            _gpu.Allocator.DestroyImage(_image, _allocation);
        }
    }

    // Copies the pixels to the image, and changes the layout of the image for the shaders. The CPU waits for the GPU.
    private void Upload(ReadOnlySpan<byte> rgbaPixels, int width, int height)
    {
        var bufferInfo = new VkBufferCreateInfo
        {
            Size = (ByteSize64)rgbaPixels.Length,
            Usage = VkBufferUsageFlags.TransferSrcBit,
            SharingMode = VkSharingMode.Exclusive,
        };
        var stagingAllocationInfo = new VmaAllocationCreateInfo
        {
            Usage = VmaMemoryUsage.Auto,
            Flags = VmaAllocationCreateFlags.HostAccessSequentialWriteBit | VmaAllocationCreateFlags.MappedBit,
        };
        VkResult result = _gpu.Allocator.CreateBuffer(bufferInfo, stagingAllocationInfo, out VkBuffer staging, out VmaAllocation stagingAllocation, out VmaAllocationInfo stagingInfo);
        if (result != VkResult.Success)
        {
            throw new InvalidOperationException($"VMA cannot create a staging buffer of {rgbaPixels.Length} bytes: {result}.");
        }

        Device device = _gpu.Device;
        VkCommandPool commandPool = default;
        VkFence fence = default;
        try
        {
            rgbaPixels.CopyTo(new Span<byte>((void*)stagingInfo.PMappedData, rgbaPixels.Length));
            _gpu.Allocator.FlushAllocation(stagingAllocation, ByteSize.Zero, (ByteSize)rgbaPixels.Length);

            var poolInfo = new VkCommandPoolCreateInfo
            {
                Flags = VkCommandPoolCreateFlags.TransientBit,
                QueueFamilyIndex = _gpu.QueueFamily,
            };
            commandPool = device.CreateCommandPool(in poolInfo, null);
            CommandBuffer commandBuffer = device.AllocateCommandBuffer(new VkCommandBufferAllocateInfo
            {
                CommandPool = commandPool,
                Level = VkCommandBufferLevel.Primary,
            });
            commandBuffer.Begin(VkCommandBufferUsageFlags.OneTimeSubmitBit);

            // The copy writes the whole image. Thus the previous content is not necessary.
            Transition(
                commandBuffer,
                VkImageLayout.Undefined,
                VkImageLayout.TransferDstOptimal,
                VkPipelineStageFlags2.None,
                VkAccessFlags2.None,
                VkPipelineStageFlags2.CopyBit,
                VkAccessFlags2.TransferWriteBit);
            var region = new VkBufferImageCopy
            {
                ImageSubresource = new VkImageSubresourceLayers { AspectMask = VkImageAspectFlags.ColorBit, LayerCount = 1 },
                ImageExtent = new VkExtent3D { Width = width, Height = height, Depth = 1 },
            };
            commandBuffer.CopyBufferToImage(staging, _image, VkImageLayout.TransferDstOptimal, [region]);

            // The fragment shader of ImGui reads the image in the next frames. It must wait for the copy.
            Transition(
                commandBuffer,
                VkImageLayout.TransferDstOptimal,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.CopyBit,
                VkAccessFlags2.TransferWriteBit,
                VkPipelineStageFlags2.FragmentShaderBit,
                VkAccessFlags2.ShaderSampledReadBit);
            commandBuffer.End();

            var fenceInfo = new VkFenceCreateInfo();
            fence = device.CreateFence(in fenceInfo, null);
            var command = new VkCommandBufferSubmitInfo { CommandBuffer = commandBuffer.Handle };
            var submit = new VkSubmitInfo2
            {
                CommandBufferInfoCount = 1,
                CommandBufferInfos = &command,
            };
            _gpu.Queue.Submit2(new Span<VkSubmitInfo2>(&submit, 1), fence);
            device.WaitForFences([fence], true, -1);
        }
        finally
        {
            if (!fence.IsNull())
            {
                device.DestroyFence(fence, null);
            }

            // The destruction of the pool also frees its command buffer.
            if (!commandPool.IsNull())
            {
                device.DestroyCommandPool(commandPool, null);
            }

            _gpu.Allocator.DestroyBuffer(staging, stagingAllocation);
        }
    }

    private void Transition(
        CommandBuffer commandBuffer,
        VkImageLayout oldLayout,
        VkImageLayout newLayout,
        VkPipelineStageFlags2 sourceStage,
        VkAccessFlags2 sourceAccess,
        VkPipelineStageFlags2 destinationStage,
        VkAccessFlags2 destinationAccess)
    {
        var barrier = new VkImageMemoryBarrier2
        {
            SrcStageMask = sourceStage,
            SrcAccessMask = sourceAccess,
            DstStageMask = destinationStage,
            DstAccessMask = destinationAccess,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
            DstQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
            Image = _image,
            SubresourceRange = _colorRange,
        };
        var dependency = new VkDependencyInfo
        {
            ImageMemoryBarrierCount = 1,
            ImageMemoryBarriers = &barrier,
        };
        commandBuffer.PipelineBarrier2(in dependency);
    }
}
