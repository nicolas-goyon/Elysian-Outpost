// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.Logging;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using ElysianOutpost.App.Ui;

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// Draws the frames of the app with Vulkan. Now each frame has the sky color and the ImGui windows.
/// </summary>
/// <remarks>
/// The renderer prepares the next frame while the GPU draws the current frame: two frames in flight.
/// Each frame in flight has its own command buffer, "image available" semaphore and fence.
/// The FIFO presentation usually limits the frame rate to the refresh rate of the screen.
/// The renderer creates the swapchain in the first call of <see cref="DrawFrame"/> that finds a visible window area.
/// </remarks>
internal sealed unsafe class VulkanRenderer : IDisposable
{
    private const int FramesInFlight = 2;

    // The sky color in sRGB (#87CEEB). The clear operation needs linear values, because the swapchain format is sRGB.
    private static readonly float4 _skyColorSrgb = new(0x87 / 255f, 0xCE / 255f, 0xEB / 255f, 1f);

    private readonly GlfwWindow _window;
    private readonly LogCategory _log;
    private readonly GpuContext _gpu;
    private readonly ImGuiPass _imGuiPass = null!;
    private Swapchain? _swapchain;
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
    /// <param name="imGui">The ImGui host. The renderer draws its frames. Release the renderer before the host.</param>
    /// <param name="log">The log category of the renderer.</param>
    /// <exception cref="InvalidOperationException">Vulkan or a suitable GPU is not available.</exception>
    public VulkanRenderer(GlfwWindow window, ImGuiHost imGui, LogCategory log)
    {
        _window = window;
        _log = log;
        _gpu = new GpuContext(window, log);
        try
        {
            CreateFrameResources();
            VkFormat format = Swapchain.SelectFormat(_gpu.PhysicalDevice, _gpu.Surface).Format;
            _imGuiPass = new ImGuiPass(_gpu, imGui, format, FramesInFlight);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the name of the GPU, for example for the debug overlay.
    /// </summary>
    public string GpuName => _gpu.GpuName;

    /// <summary>
    /// Gets the time to record the ImGui commands in the last call of <see cref="DrawFrame"/>.
    /// The value is zero if the last call did not draw ImGui.
    /// </summary>
    public TimeSpan ImGuiRecordTime { get; private set; }

    /// <summary>
    /// Draws and presents one frame.
    /// </summary>
    /// <param name="imGuiDrawData">The draw data of the ImGui frame to draw after the scene, or a null pointer for no ImGui frame.</param>
    /// <returns><c>true</c> if the renderer presented a frame. <c>false</c> if the window has no visible area, for example a minimized window.</returns>
    public bool DrawFrame(ImDrawDataPtr imGuiDrawData)
    {
        ImGuiRecordTime = TimeSpan.Zero;
        int2 size = _window.FramebufferSize;
        if (size.X <= 0 || size.Y <= 0)
        {
            return false;
        }

        // A window can start without a visible area, for example a minimized window. Thus the first visible frame creates the swapchain.
        Swapchain swapchain = _swapchain ??= CreateSwapchain(size);

        // The window system can report the new size before the swapchain becomes out of date.
        if (_swapchainOutdated || size.X != _swapchainFramebufferSize.X || size.Y != _swapchainFramebufferSize.Y)
        {
            RecreateSwapchain(swapchain, size);
        }

        VkFence fence = _inFlight[_frame];
        _gpu.Device.WaitForFences([fence], true, -1);

        int imageIndex;
        try
        {
            VkResult acquired = _gpu.Device.AcquireNextImageKHR(swapchain.Handle, -1, _imageAvailable[_frame], VkFence.NullHandle, out imageIndex);

            // A suboptimal swapchain can still show this frame. The next call of DrawFrame creates the swapchain again.
            if (acquired == VkResult.SuboptimalKHR)
            {
                _swapchainOutdated = true;
            }
        }
        catch (VkResultError error) when (error.Result == VkResult.ErrorOutOfDateKHR)
        {
            // The swapchain does not match the window anymore. The semaphore is not signaled, thus the next frame can use it.
            RecreateSwapchain(swapchain, size);
            return false;
        }

        // Reset the fence only now. If the acquisition fails, the fence stays signaled for the next call.
        _gpu.Device.ResetFences([fence]);
        Record(swapchain, _commandBuffers[_frame], imageIndex, imGuiDrawData);
        Submit(_commandBuffers[_frame], _imageAvailable[_frame], swapchain.RenderingFinished[imageIndex], fence);
        bool presented = Present(swapchain, imageIndex);
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

            // The framebuffers of the ImGui pass use the image views of the swapchain. Thus the pass goes first.
            _imGuiPass?.Dispose();
            _swapchain?.Dispose();
        }

        bool validation = _gpu.ValidationEnabled;
        _gpu.Dispose();

        // Without the validation layers, the totals have no meaning. Thus the renderer writes them only with the layers.
        if (validation)
        {
            _log.Info($"The Vulkan validation layers reported {ValidationLog.ErrorCount} error and {ValidationLog.WarningCount} warning messages.");
        }
    }

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

    private Swapchain CreateSwapchain(int2 size)
    {
        var swapchain = new Swapchain(_gpu, size);
        _imGuiPass.CreateFramebuffers(swapchain);
        _swapchainFramebufferSize = size;
        _swapchainOutdated = false;
        _log.Info($"The swapchain is ready: {swapchain.Images.Length} images of {swapchain.Extent.Width}x{swapchain.Extent.Height} pixels, format {swapchain.Format}, FIFO presentation.");
        return swapchain;
    }

    private void RecreateSwapchain(Swapchain swapchain, int2 size)
    {
        // The framebuffers of the ImGui pass use the image views of the swapchain.
        // The GPU must be idle before the release of the framebuffers.
        _gpu.Device.WaitIdle();
        _imGuiPass.DestroyFramebuffers();
        swapchain.Recreate(size);
        _imGuiPass.CreateFramebuffers(swapchain);
        _swapchainFramebufferSize = size;
        _swapchainOutdated = false;
        _log.Info($"The swapchain changes to {swapchain.Extent.Width}x{swapchain.Extent.Height} pixels.");
    }

    private void Record(Swapchain swapchain, CommandBuffer commandBuffer, int imageIndex, ImDrawDataPtr imGuiDrawData)
    {
        VkImage image = swapchain.Images[imageIndex];
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

        float4 clear = ColorSpace.IsSrgb(swapchain.Format) ? ColorSpace.SrgbToLinear(_skyColorSrgb) : _skyColorSrgb;
        var colorAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = swapchain.ImageViews[imageIndex],
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.Store,
            ClearValue = new VkClearValue { Color = new VkClearColorValue { Float32 = clear } },
        };
        var renderingInfo = new VkRenderingInfo
        {
            RenderArea = new VkRect2D(swapchain.Extent),
            LayerCount = 1,
            ColorAttachmentCount = 1,
            ColorAttachments = &colorAttachment,
        };
        commandBuffer.BeginRendering(in renderingInfo);

        // The chunk renderer (#39) draws here.
        commandBuffer.EndRendering();

        // ImGui draws over the scene, in its own render pass. See ImGuiPass.
        if (!imGuiDrawData.IsNull())
        {
            long start = Stopwatch.GetTimestamp();
            _imGuiPass.Record(commandBuffer, imageIndex, swapchain.Extent, imGuiDrawData);
            ImGuiRecordTime = Stopwatch.GetElapsedTime(start);
        }

        // The signal of the "rendering finished" semaphore must wait for this layout change.
        // Thus the barrier has a destination stage. Without it, the synchronization validation finds a hazard.
        TransitionImage(
            commandBuffer,
            image,
            VkImageLayout.ColorAttachmentOptimal,
            VkImageLayout.PresentSrcKHR,
            VkPipelineStageFlags2.ColorAttachmentOutputBit,
            VkAccessFlags2.ColorAttachmentWriteBit,
            VkPipelineStageFlags2.ColorAttachmentOutputBit,
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

    private bool Present(Swapchain swapchain, int imageIndex)
    {
        VkSwapchainKHR handle = swapchain.Handle;
        VkSemaphore renderingFinished = swapchain.RenderingFinished[imageIndex];
        var presentInfo = new VkPresentInfoKHR
        {
            WaitSemaphoreCount = 1,
            WaitSemaphores = &renderingFinished,
            SwapchainCount = 1,
            Swapchains = &handle,
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
