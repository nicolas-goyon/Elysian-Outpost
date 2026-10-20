// SPDX-License-Identifier: MPL-2.0

using Brutal.GlfwApi;
using Brutal.Logging;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// Draws the frames of the app with Vulkan. Now each frame has only the sky color.
/// </summary>
/// <remarks>
/// The renderer prepares the next frame while the GPU draws the current frame: two frames in flight.
/// Each frame in flight has its own command buffer, "image available" semaphore and fence.
/// The FIFO presentation limits the frame rate to the refresh rate of the screen.
/// </remarks>
internal sealed unsafe class VulkanRenderer : IDisposable
{
    private const int FramesInFlight = 2;

    // The sky color in sRGB (#87CEEB). The clear operation needs linear values, because the swapchain format is sRGB.
    private static readonly float4 _skyColorSrgb = new(0x87 / 255f, 0xCE / 255f, 0xEB / 255f, 1f);

    private readonly GlfwWindow _window;
    private readonly LogCategory _log;
    private readonly GpuContext _gpu;
    private readonly Swapchain _swapchain;
    private readonly VkCommandPool[] _commandPools = new VkCommandPool[FramesInFlight];
    private readonly CommandBuffer[] _commandBuffers = new CommandBuffer[FramesInFlight];
    private readonly VkSemaphore[] _imageAvailable = new VkSemaphore[FramesInFlight];
    private readonly VkFence[] _inFlight = new VkFence[FramesInFlight];
    private int _frame;
    private int2 _swapchainFramebufferSize;
    private bool _swapchainOutdated;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanRenderer"/> class for a window.
    /// </summary>
    /// <param name="window">The window. It must not have an OpenGL context.</param>
    /// <param name="log">The log category of the renderer.</param>
    /// <exception cref="InvalidOperationException">Vulkan or a suitable GPU is not available.</exception>
    public VulkanRenderer(GlfwWindow window, LogCategory log)
    {
        _window = window;
        _log = log;
        _gpu = new GpuContext(window, log);
        try
        {
            _swapchainFramebufferSize = window.FramebufferSize;
            _swapchain = new Swapchain(_gpu, ValidSize(_swapchainFramebufferSize));
            CreateFrameResources();
            _log.Info($"The swapchain is ready: {_swapchain.Images.Length} images of {_swapchain.Extent.Width}x{_swapchain.Extent.Height} pixels, format {_swapchain.Format}, FIFO presentation.");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Draws and presents one frame.
    /// </summary>
    /// <returns><c>true</c> if the renderer presented a frame. <c>false</c> if the window has no visible area, for example a minimized window.</returns>
    public bool DrawFrame()
    {
        int2 size = _window.FramebufferSize;
        if (size.X <= 0 || size.Y <= 0)
        {
            return false;
        }

        // The window system can report the new size before the swapchain becomes out of date.
        if (_swapchainOutdated || size.X != _swapchainFramebufferSize.X || size.Y != _swapchainFramebufferSize.Y)
        {
            RecreateSwapchain(size);
        }

        VkFence fence = _inFlight[_frame];
        _gpu.Device.WaitForFences([fence], true, -1);

        int imageIndex;
        try
        {
            _gpu.Device.AcquireNextImageKHR(_swapchain.Handle, -1, _imageAvailable[_frame], VkFence.NullHandle, out imageIndex);
        }
        catch (VkResultError error) when (error.Result == VkResult.ErrorOutOfDateKHR)
        {
            // The swapchain does not match the window anymore. The semaphore is not signaled, thus the next frame can use it.
            RecreateSwapchain(size);
            return false;
        }

        // Reset the fence only now. If the acquisition fails, the fence stays signaled for the next call.
        _gpu.Device.ResetFences([fence]);
        Record(_commandBuffers[_frame], imageIndex);
        Submit(_commandBuffers[_frame], _imageAvailable[_frame], _swapchain.RenderingFinished[imageIndex], fence);
        bool presented = Present(imageIndex);
        _frame = (_frame + 1) % FramesInFlight;
        return presented;
    }

    /// <summary>
    /// Waits until the GPU is idle, then releases all the Vulkan objects.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_gpu.Device is not null)
        {
            _gpu.Device.WaitIdle();
            DestroyFrameResources();
            _swapchain?.Dispose();
        }

        _gpu.Dispose();
#if DEBUG
        _log.Info($"The Vulkan validation layers reported {ValidationLog.ErrorCount} error and {ValidationLog.WarningCount} warning messages.");
#endif
    }

    // A swapchain needs a size of 1 pixel or more. DrawFrame does not draw if the window has no visible area.
    private static int2 ValidSize(int2 size) => new(Math.Max(size.X, 1), Math.Max(size.Y, 1));

    // Converts one sRGB color component to a linear value (IEC 61966-2-1).
    private static float SrgbToLinear(float value) =>
        value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);

    private void CreateFrameResources()
    {
        Device device = _gpu.Device;
        var poolInfo = new VkCommandPoolCreateInfo
        {
            Flags = VkCommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = _gpu.QueueFamily,
        };
        var semaphoreInfo = new VkSemaphoreCreateInfo();

        // The fence starts signaled. Thus the first wait of each frame in flight returns immediately.
        var fenceInfo = new VkFenceCreateInfo { Flags = VkFenceCreateFlags.SignaledBit };
        for (int i = 0; i < FramesInFlight; i++)
        {
            _commandPools[i] = device.CreateCommandPool(in poolInfo, null);
            _commandBuffers[i] = device.AllocateCommandBuffer(new VkCommandBufferAllocateInfo
            {
                CommandPool = _commandPools[i],
                Level = VkCommandBufferLevel.Primary,
            });
            _imageAvailable[i] = device.CreateSemaphore(in semaphoreInfo, null);
            _inFlight[i] = device.CreateFence(in fenceInfo, null);
        }
    }

    private void DestroyFrameResources()
    {
        Device device = _gpu.Device;
        for (int i = 0; i < FramesInFlight; i++)
        {
            // The destruction of a command pool also frees its command buffers.
            if (!_commandPools[i].IsNull())
            {
                device.DestroyCommandPool(_commandPools[i], null);
            }

            if (!_imageAvailable[i].IsNull())
            {
                device.DestroySemaphore(_imageAvailable[i], null);
            }

            if (!_inFlight[i].IsNull())
            {
                device.DestroyFence(_inFlight[i], null);
            }
        }
    }

    private void RecreateSwapchain(int2 size)
    {
        _swapchain.Recreate(ValidSize(size));
        _swapchainFramebufferSize = size;
        _swapchainOutdated = false;
        _log.Info($"The swapchain changes to {_swapchain.Extent.Width}x{_swapchain.Extent.Height} pixels.");
    }

    private void Record(CommandBuffer commandBuffer, int imageIndex)
    {
        VkImage image = _swapchain.Images[imageIndex];
        commandBuffer.Reset();
        commandBuffer.Begin(VkCommandBufferUsageFlags.OneTimeSubmitBit);

        // The previous content of the image is not necessary, because the clear operation replaces it.
        TransitionImage(
            commandBuffer,
            image,
            VkImageLayout.Undefined,
            VkImageLayout.ColorAttachmentOptimal,
            VkPipelineStageFlags2.ColorAttachmentOutputBit,
            VkAccessFlags2.None,
            VkPipelineStageFlags2.ColorAttachmentOutputBit,
            VkAccessFlags2.ColorAttachmentWriteBit);

        float4 clear = _swapchain.Format is VkFormat.B8G8R8A8SRGB or VkFormat.R8G8B8A8SRGB
            ? new float4(SrgbToLinear(_skyColorSrgb.X), SrgbToLinear(_skyColorSrgb.Y), SrgbToLinear(_skyColorSrgb.Z), 1f)
            : _skyColorSrgb;
        var colorAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = _swapchain.ImageViews[imageIndex],
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.Store,
            ClearValue = new VkClearValue { Color = new VkClearColorValue { Float32 = clear } },
        };
        var renderingInfo = new VkRenderingInfo
        {
            RenderArea = new VkRect2D(_swapchain.Extent),
            LayerCount = 1,
            ColorAttachmentCount = 1,
            ColorAttachments = &colorAttachment,
        };
        commandBuffer.BeginRendering(in renderingInfo);

        // The next renderers draw here: the chunk meshes (#39) and the debug overlay (#36).
        commandBuffer.EndRendering();

        TransitionImage(
            commandBuffer,
            image,
            VkImageLayout.ColorAttachmentOptimal,
            VkImageLayout.PresentSrcKHR,
            VkPipelineStageFlags2.ColorAttachmentOutputBit,
            VkAccessFlags2.ColorAttachmentWriteBit,
            VkPipelineStageFlags2.None,
            VkAccessFlags2.None);
        commandBuffer.End();
    }

    private static void TransitionImage(
        CommandBuffer commandBuffer,
        VkImage image,
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
            Image = image,
            SubresourceRange = new VkImageSubresourceRange
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                LevelCount = 1,
                LayerCount = 1,
            },
        };
        var dependency = new VkDependencyInfo
        {
            ImageMemoryBarrierCount = 1,
            ImageMemoryBarriers = &barrier,
        };
        commandBuffer.PipelineBarrier2(in dependency);
    }

    private void Submit(CommandBuffer commandBuffer, VkSemaphore imageAvailable, VkSemaphore renderingFinished, VkFence fence)
    {
        // The color output waits for the acquired image. The semaphore signal waits for all the commands.
        var wait = new VkSemaphoreSubmitInfo
        {
            Semaphore = imageAvailable,
            StageMask = VkPipelineStageFlags2.ColorAttachmentOutputBit,
        };
        var signal = new VkSemaphoreSubmitInfo
        {
            Semaphore = renderingFinished,
            StageMask = VkPipelineStageFlags2.AllCommandsBit,
        };
        var command = new VkCommandBufferSubmitInfo { CommandBuffer = commandBuffer.Handle };
        var submit = new VkSubmitInfo2
        {
            WaitSemaphoreInfoCount = 1,
            WaitSemaphoreInfos = &wait,
            CommandBufferInfoCount = 1,
            CommandBufferInfos = &command,
            SignalSemaphoreInfoCount = 1,
            SignalSemaphoreInfos = &signal,
        };
        _gpu.Queue.Submit2(new Span<VkSubmitInfo2>(&submit, 1), fence);
    }

    private bool Present(int imageIndex)
    {
        VkSwapchainKHR swapchain = _swapchain.Handle;
        VkSemaphore renderingFinished = _swapchain.RenderingFinished[imageIndex];
        var presentInfo = new VkPresentInfoKHR
        {
            WaitSemaphoreCount = 1,
            WaitSemaphores = &renderingFinished,
            SwapchainCount = 1,
            Swapchains = &swapchain,
            ImageIndices = &imageIndex,
        };

        VkResult result;
        try
        {
            result = _gpu.Queue.PresentKHR(presentInfo);
        }
        catch (VkResultError error) when (error.Result == VkResult.ErrorOutOfDateKHR)
        {
            // The presentation still waits for the semaphore. Thus the semaphore can be used again.
            result = VkResult.ErrorOutOfDateKHR;
        }

        // The next call of DrawFrame creates the swapchain again. At that time, the window size is valid.
        if (result is VkResult.SuboptimalKHR or VkResult.ErrorOutOfDateKHR)
        {
            _swapchainOutdated = true;
        }

        return result != VkResult.ErrorOutOfDateKHR;
    }
}
