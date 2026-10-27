// SPDX-License-Identifier: MPL-2.0

using Brutal.ImGuiApi;
using Brutal.ImGuiApi.Backends;
using Brutal.VulkanApi;
using ElysianOutpost.App.Ui;

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// Draws the ImGui frames on the swapchain images, after the scene.
/// </summary>
/// <remarks>
/// The Vulkan backend of BRUTAL ImGui needs a render pass. It does not support dynamic rendering.
/// Thus this class has a render pass with one color attachment and one framebuffer for each swapchain image.
/// The render pass loads the image of the scene and keeps the layout <see cref="VkImageLayout.ColorAttachmentOptimal"/>.
/// The renderer changes the layout for the presentation after the render pass.
/// ImGui needs the backend before its first frame, because the backend builds the font texture.
/// Thus the renderer creates this pass before the swapchain, with the format that the swapchain will use.
/// </remarks>
internal sealed unsafe class ImGuiPass : IDisposable
{
    // The backend needs at least 8 descriptor sets. ImGui uses one texture for the fonts, and the start screen uses one for the logo.
    private const int DescriptorPoolSize = 8;

    private readonly GpuContext _gpu;
    private readonly VkFormat _format;
    private readonly VkRenderPass _renderPass;
    private readonly VulkanBackend? _backend;
    private VkFramebuffer[] _framebuffers = [];
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImGuiPass"/> class. Call <see cref="CreateFramebuffers"/> before the first frame.
    /// </summary>
    /// <param name="gpu">The Vulkan objects of the window.</param>
    /// <param name="imGui">The ImGui host. The backend uses its ImGui context.</param>
    /// <param name="format">The format of the swapchain images.</param>
    /// <param name="framesInFlight">The number of frames in flight. The backend keeps vertex buffers for each of them.</param>
    public ImGuiPass(GpuContext gpu, ImGuiHost imGui, VkFormat format, int framesInFlight)
    {
        _gpu = gpu;
        _format = format;
        _renderPass = CreateRenderPass(gpu.Device, format);
        try
        {
            // The backend has ImageCount sets of vertex and index buffers, and each draw uses the next set.
            // With two sets, a draw uses the set of a draw that is two or more frames old.
            // The renderer waited for the fence of that frame, thus the GPU does not use the set anymore.
            // MinImageCount applies only to the windows of the multi-viewport mode of ImGui. The app does not use that mode.
            _backend = new VulkanBackend(new VulkanBackend.CreateInfo
            {
                Device = gpu.Device,
                GraphicsQueue = gpu.Queue,
                RenderPass = _renderPass,
                MinImageCount = Math.Max(2, framesInFlight),
                ImageCount = Math.Max(2, framesInFlight),
                SampleCount = VkSampleCountFlags._1Bit,
                DescriptorPoolSize = DescriptorPoolSize,
            });

            // The ImGui colors are sRGB values. The shaders of the backend write them without a conversion.
            // With an sRGB swapchain, the GPU then converts them again, and they become too light.
            if (ColorSpace.IsSrgb(format))
            {
                imGui.UseLinearColors();
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the format of the swapchain images that the pass draws on.
    /// </summary>
    public VkFormat Format => _format;

    /// <summary>
    /// Gives ImGui access to an image, for example for <c>ImDrawList.AddImage</c>. The backend makes a descriptor set for the image.
    /// </summary>
    /// <param name="sampler">The sampler of the image.</param>
    /// <param name="imageView">The view of the image. The image must have the layout <see cref="VkImageLayout.ShaderReadOnlyOptimal"/>.</param>
    /// <returns>The reference of the texture for ImGui. Call <see cref="RemoveTexture"/> before the release of the image.</returns>
    public ImTextureRef AddTexture(VkSampler sampler, VkImageView imageView) =>
        _backend!.AddTexture(sampler, imageView, VkImageLayout.ShaderReadOnlyOptimal);

    /// <summary>
    /// Releases the descriptor set of a texture that <see cref="AddTexture"/> made. The GPU must not use it.
    /// </summary>
    /// <param name="texture">The reference of the texture.</param>
    public void RemoveTexture(ImTextureRef texture) => _backend!.RemoveTexture(texture);

    /// <summary>
    /// Creates one framebuffer for each image of the swapchain. Call it after each creation of the swapchain.
    /// </summary>
    /// <param name="swapchain">The swapchain. Its format must not change.</param>
    /// <exception cref="InvalidOperationException">The format of the swapchain changed.</exception>
    public void CreateFramebuffers(Swapchain swapchain)
    {
        // The render pass and the pipeline of the backend use the format.
        // The swapchain and the renderer use the same method to select the format. Thus this error is not expected.
        if (swapchain.Format != _format)
        {
            throw new InvalidOperationException($"The swapchain format changed from {_format} to {swapchain.Format}. The ImGui pass cannot use the new format.");
        }

        DestroyFramebuffers();
        _framebuffers = new VkFramebuffer[swapchain.ImageViews.Length];
        for (int i = 0; i < _framebuffers.Length; i++)
        {
            VkImageView view = swapchain.ImageViews[i];
            var info = new VkFramebufferCreateInfo
            {
                RenderPass = _renderPass,
                AttachmentCount = 1,
                Attachments = &view,
                Width = swapchain.Extent.Width,
                Height = swapchain.Extent.Height,
                Layers = 1,
            };
            _framebuffers[i] = _gpu.Device.CreateFramebuffer(in info, null);
        }
    }

    /// <summary>
    /// Releases the framebuffers. Call it before the swapchain releases its image views. The GPU must not use the framebuffers.
    /// </summary>
    public void DestroyFramebuffers()
    {
        foreach (VkFramebuffer framebuffer in _framebuffers)
        {
            _gpu.Device.DestroyFramebuffer(framebuffer, null);
        }

        _framebuffers = [];
    }

    /// <summary>
    /// Records the commands that draw an ImGui frame on a swapchain image.
    /// </summary>
    /// <param name="commandBuffer">The command buffer, in the recording state and outside a render pass.</param>
    /// <param name="imageIndex">The index of the swapchain image. The image must have the layout <see cref="VkImageLayout.ColorAttachmentOptimal"/>.</param>
    /// <param name="extent">The size of the swapchain images.</param>
    /// <param name="drawData">The draw data of the ImGui frame.</param>
    public void Record(CommandBuffer commandBuffer, int imageIndex, VkExtent2D extent, ImDrawDataPtr drawData)
    {
        var beginInfo = new VkRenderPassBeginInfo
        {
            RenderPass = _renderPass,
            Framebuffer = _framebuffers[imageIndex],
            RenderArea = new VkRect2D(extent),
        };
        commandBuffer.BeginRenderPass(in beginInfo, VkSubpassContents.Inline);
        _backend!.RenderDrawData(drawData, commandBuffer);
        commandBuffer.EndRenderPass();
    }

    /// <summary>
    /// Releases the backend, the framebuffers and the render pass. The GPU must not use them.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _backend?.Dispose();
        DestroyFramebuffers();
        if (!_renderPass.IsNull())
        {
            _gpu.Device.DestroyRenderPass(_renderPass, null);
        }
    }

    private static VkRenderPass CreateRenderPass(Device device, VkFormat format)
    {
        // LOAD keeps the scene. The layout does not change in the render pass.
        var attachment = new VkAttachmentDescription
        {
            Format = format,
            Samples = VkSampleCountFlags._1Bit,
            LoadOp = VkAttachmentLoadOp.Load,
            StoreOp = VkAttachmentStoreOp.Store,
            StencilLoadOp = VkAttachmentLoadOp.DontCare,
            StencilStoreOp = VkAttachmentStoreOp.DontCare,
            InitialLayout = VkImageLayout.ColorAttachmentOptimal,
            FinalLayout = VkImageLayout.ColorAttachmentOptimal,
        };
        var colorReference = new VkAttachmentReference
        {
            Attachment = 0,
            Layout = VkImageLayout.ColorAttachmentOptimal,
        };
        var subpass = new VkSubpassDescription
        {
            PipelineBindPoint = VkPipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            ColorAttachments = &colorReference,
        };

        // The load operation and the blend operations read the image. They must wait for the writes of the scene.
        // The barrier after the render pass makes the writes of ImGui available for the presentation.
        var dependency = new VkSubpassDependency
        {
            SrcSubpass = VK.SUBPASS_EXTERNAL,
            DstSubpass = 0,
            SrcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = VkAccessFlags.ColorAttachmentWriteBit,
            DstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit,
            DstAccessMask = VkAccessFlags.ColorAttachmentReadBit | VkAccessFlags.ColorAttachmentWriteBit,
        };
        var info = new VkRenderPassCreateInfo
        {
            AttachmentCount = 1,
            Attachments = &attachment,
            SubpassCount = 1,
            Subpasses = &subpass,
            DependencyCount = 1,
            Dependencies = &dependency,
        };
        return device.CreateRenderPass(in info, null);
    }
}
