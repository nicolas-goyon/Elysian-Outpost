// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;
using Brutal;
using Brutal.Numerics;
using Brutal.ShaderCApi;
using Brutal.VulkanApi;

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// The graphics pipeline of the selection box: black lines on the 12 edges of the selected voxel (#40).
/// </summary>
/// <remarks>
/// <para>
/// The vertex shader makes the vertices of the edges. Thus the pipeline has no vertex input, and a draw uses 24 vertices.
/// The push constants have the view-projection matrix (bytes 0 to 63) and the box (bytes 64 to 79), as in <c>selection.vert</c>.
/// </para>
/// <para>
/// The box is a little larger than the voxel (<see cref="Margin"/>), and the vertex shader moves the lines a little nearer to the camera.
/// Thus the edges are in front of the faces of the voxel. The depth test hides the edges behind the voxel or behind other voxels,
/// if they are farther than the offset behind them (see <c>selection.vert</c>).
/// The lines do not write the depth.
/// </para>
/// </remarks>
internal sealed unsafe class SelectionBoxPipeline : IDisposable
{
    /// <summary>
    /// The distance between the faces of the voxel and the box, in voxels.
    /// </summary>
    public const float Margin = 0.005f;

    private const int PushConstantSize = 80;
    private const int BoxOffset = 64;
    private const int VertexCount = 24;

    private readonly GpuContext _gpu;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelectionBoxPipeline"/> class. It compiles the shaders.
    /// </summary>
    /// <param name="gpu">The Vulkan objects of the window.</param>
    /// <param name="colorFormat">The format of the swapchain images.</param>
    /// <param name="depthFormat">The format of the depth image.</param>
    /// <param name="compiler">The shader compiler. The pipelines of the renderer use the same compiler, because the first compilation of a compiler is slow.</param>
    public SelectionBoxPipeline(GpuContext gpu, VkFormat colorFormat, VkFormat depthFormat, ShaderCompiler compiler)
    {
        _gpu = gpu;
        var range = new VkPushConstantRange
        {
            StageFlags = VkShaderStageFlags.VertexBit,
            Offset = default,
            Size = (ByteSize32)PushConstantSize,
        };
        var layoutInfo = new VkPipelineLayoutCreateInfo
        {
            PushConstantRangeCount = 1,
            PushConstantRanges = &range,
        };
        Layout = gpu.Device.CreatePipelineLayout(in layoutInfo, null);
        try
        {
            Pipeline = CreatePipeline(gpu.Device, Layout, colorFormat, depthFormat, compiler);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the pipeline layout.
    /// </summary>
    public VkPipelineLayout Layout { get; }

    /// <summary>
    /// Gets the graphics pipeline.
    /// </summary>
    public VkPipeline Pipeline { get; }

    /// <summary>
    /// Records the draw of the box around one voxel. Call it in the dynamic rendering of the scene, after the chunks.
    /// The viewport and the scissor of the chunks stay valid.
    /// </summary>
    /// <param name="commandBuffer">The command buffer of the frame.</param>
    /// <param name="viewProjection">The view-projection matrix of the frame.</param>
    /// <param name="voxel">The world position of the voxel.</param>
    public void Record(CommandBuffer commandBuffer, float4x4 viewProjection, int3 voxel)
    {
        float4 box = new(voxel.X - Margin, voxel.Y - Margin, voxel.Z - Margin, 1f + (2f * Margin));
        commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, Pipeline);
        commandBuffer.PushConstants(Layout, VkShaderStageFlags.VertexBit, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<float4x4>(ref viewProjection)));
        commandBuffer.PushConstants(Layout, VkShaderStageFlags.VertexBit, BoxOffset, MemoryMarshal.AsBytes(new ReadOnlySpan<float4>(ref box)));
        commandBuffer.Draw(VertexCount, 1, 0, 0);
    }

    /// <summary>
    /// Releases the pipeline and its layout. The GPU must not use them.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!Pipeline.IsNull())
        {
            _gpu.Device.DestroyPipeline(Pipeline, null);
        }

        if (!Layout.IsNull())
        {
            _gpu.Device.DestroyPipelineLayout(Layout, null);
        }
    }

    private static VkPipeline CreatePipeline(Device device, VkPipelineLayout layout, VkFormat colorFormat, VkFormat depthFormat, ShaderCompiler compiler)
    {
        VkShaderModule vertexShader = compiler.CreateModule(device, "selection.vert", ShaderKind.VertexShader);
        VkShaderModule fragmentShader = VkShaderModule.NullHandle;
        try
        {
            fragmentShader = compiler.CreateModule(device, "selection.frag", ShaderKind.FragmentShader);
            return CreatePipeline(device, layout, colorFormat, depthFormat, vertexShader, fragmentShader);
        }
        finally
        {
            // The pipeline keeps its own copy of the shader code.
            device.DestroyShaderModule(vertexShader, null);
            if (!fragmentShader.IsNull())
            {
                device.DestroyShaderModule(fragmentShader, null);
            }
        }
    }

    private static VkPipeline CreatePipeline(
        Device device,
        VkPipelineLayout layout,
        VkFormat colorFormat,
        VkFormat depthFormat,
        VkShaderModule vertexShader,
        VkShaderModule fragmentShader)
    {
        byte* entryPoint = stackalloc byte[] { (byte)'m', (byte)'a', (byte)'i', (byte)'n', 0 };
        VkPipelineShaderStageCreateInfo* stages = stackalloc VkPipelineShaderStageCreateInfo[2];
        stages[0] = new VkPipelineShaderStageCreateInfo
        {
            Stage = VkShaderStageFlags.VertexBit,
            Module = vertexShader,
            Name = entryPoint,
        };
        stages[1] = new VkPipelineShaderStageCreateInfo
        {
            Stage = VkShaderStageFlags.FragmentBit,
            Module = fragmentShader,
            Name = entryPoint,
        };

        var vertexInput = new VkPipelineVertexInputStateCreateInfo();
        var inputAssembly = new VkPipelineInputAssemblyStateCreateInfo { Topology = VkPrimitiveTopology.LineList };
        var viewport = new VkPipelineViewportStateCreateInfo { ViewportCount = 1, ScissorCount = 1 };

        // A line width of 1 needs no device feature (wideLines).
        var rasterization = new VkPipelineRasterizationStateCreateInfo
        {
            PolygonMode = VkPolygonMode.Fill,
            CullMode = VkCullModeFlags.None,
            FrontFace = VkFrontFace.Clockwise,
            LineWidth = 1f,
        };
        var multisample = new VkPipelineMultisampleStateCreateInfo { RasterizationSamples = VkSampleCountFlags._1Bit };
        var depthStencil = new VkPipelineDepthStencilStateCreateInfo
        {
            DepthTestEnable = true,
            DepthWriteEnable = false,
            DepthCompareOp = VkCompareOp.LessOrEqual,
        };
        var blendAttachment = new VkPipelineColorBlendAttachmentState
        {
            BlendEnable = false,
            ColorWriteMask = VkColorComponentFlags.RBit | VkColorComponentFlags.GBit | VkColorComponentFlags.BBit | VkColorComponentFlags.ABit,
        };
        var colorBlend = new VkPipelineColorBlendStateCreateInfo
        {
            AttachmentCount = 1,
            Attachments = &blendAttachment,
        };
        VkDynamicState* dynamicStates = stackalloc VkDynamicState[] { VkDynamicState.Viewport, VkDynamicState.Scissor };
        var dynamicState = new VkPipelineDynamicStateCreateInfo
        {
            DynamicStateCount = 2,
            DynamicStates = dynamicStates,
        };
        var rendering = new VkPipelineRenderingCreateInfo
        {
            ColorAttachmentCount = 1,
            ColorAttachmentFormats = &colorFormat,
            DepthAttachmentFormat = depthFormat,
        };
        var info = new VkGraphicsPipelineCreateInfo
        {
            Next = &rendering,
            StageCount = 2,
            Stages = stages,
            VertexInputState = &vertexInput,
            InputAssemblyState = &inputAssembly,
            ViewportState = &viewport,
            RasterizationState = &rasterization,
            MultisampleState = &multisample,
            DepthStencilState = &depthStencil,
            ColorBlendState = &colorBlend,
            DynamicState = &dynamicState,
            Layout = layout,
        };
        VkPipeline pipeline;
        device.CreateGraphicsPipelines(VkPipelineCache.NullHandle, new ReadOnlySpan<VkGraphicsPipelineCreateInfo>(&info, 1), null, new ReadOnlySpan<VkPipeline>(&pipeline, 1));
        return pipeline;
    }
}
