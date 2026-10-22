// SPDX-License-Identifier: MPL-2.0

using Brutal;
using Brutal.Numerics;
using Brutal.ShaderCApi;
using Brutal.VulkanApi;
using ElysianOutpost.Core.Lighting;
using ElysianOutpost.Core.Meshing;

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// The graphics pipeline of the chunk meshes and its pipeline layout.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline uses dynamic rendering, with the format of the swapchain images and the format of the depth image.
/// The viewport and the scissor are dynamic states, thus a change of the window size does not change the pipeline.
/// </para>
/// <para>
/// The faces that point to the camera are clockwise on the screen (see <c>ScreenWindingTests</c>).
/// Thus the front face is <see cref="VkFrontFace.Clockwise"/>, and the pipeline does not draw the back faces.
/// </para>
/// <para>
/// The push constants have the view-projection matrix (bytes 0 to 63) and the world position of the chunk (bytes 64 to 79).
/// The light values of <see cref="SceneLighting"/> are specialization constants of the fragment shader.
/// </para>
/// <para>
/// With an sRGB swapchain format, the GPU converts the linear output of the fragment shader to sRGB.
/// With another format, for example a UNORM format, the fragment shader does the conversion. Thus the colors are the same.
/// </para>
/// </remarks>
internal sealed unsafe class ChunkPipeline : IDisposable
{
    /// <summary>
    /// The size of the push constants, in bytes.
    /// </summary>
    public const int PushConstantSize = 80;

    /// <summary>
    /// The offset of the chunk position in the push constants, in bytes.
    /// </summary>
    public const int ChunkPositionOffset = 64;

    // Thirty-four values: the direction to the light (3 floats), the light color (3 floats),
    // the nine ambient coefficients (27 floats), and the sRGB conversion (a 32-bit boolean).
    private const int SpecializationConstantCount = 34;

    private readonly GpuContext _gpu;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkPipeline"/> class. It compiles the shaders.
    /// </summary>
    /// <param name="gpu">The Vulkan objects of the window.</param>
    /// <param name="colorFormat">The format of the swapchain images.</param>
    /// <param name="depthFormat">The format of the depth image.</param>
    /// <param name="lighting">The light of the scene.</param>
    public ChunkPipeline(GpuContext gpu, VkFormat colorFormat, VkFormat depthFormat, SceneLighting lighting)
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
            Pipeline = CreatePipeline(gpu.Device, Layout, colorFormat, depthFormat, lighting);
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

    private static VkPipeline CreatePipeline(Device device, VkPipelineLayout layout, VkFormat colorFormat, VkFormat depthFormat, SceneLighting lighting)
    {
        using var compiler = new ShaderCompiler();
        VkShaderModule vertexShader = compiler.CreateModule(device, "chunk.vert", ShaderKind.VertexShader);
        VkShaderModule fragmentShader = VkShaderModule.NullHandle;
        try
        {
            fragmentShader = compiler.CreateModule(device, "chunk.frag", ShaderKind.FragmentShader);
            return CreatePipeline(device, layout, colorFormat, depthFormat, lighting, vertexShader, fragmentShader);
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
        SceneLighting lighting,
        VkShaderModule vertexShader,
        VkShaderModule fragmentShader)
    {
        uint* values = stackalloc uint[SpecializationConstantCount];
        SpecializationValues(lighting, encodeSrgb: !ColorSpace.IsSrgb(colorFormat)).CopyTo(new Span<uint>(values, SpecializationConstantCount));
        VkSpecializationMapEntry* entries = stackalloc VkSpecializationMapEntry[SpecializationConstantCount];
        for (int i = 0; i < SpecializationConstantCount; i++)
        {
            entries[i] = new VkSpecializationMapEntry
            {
                ConstantID = i,
                Offset = (ByteSize32)(i * sizeof(uint)),
                Size = (ByteSize64)sizeof(uint),
            };
        }

        var specialization = new VkSpecializationInfo
        {
            MapEntryCount = SpecializationConstantCount,
            MapEntries = entries,
            DataSize = (ByteSize64)(SpecializationConstantCount * sizeof(uint)),
            Data = values,
        };

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
            SpecializationInfo = &specialization,
        };

        // The layout of ChunkVertex: the position (3 floats), the normal (3 floats) and the color (4 bytes).
        var binding = new VkVertexInputBindingDescription
        {
            Binding = 0,
            Stride = (ByteSize32)ChunkVertex.SizeInBytes,
            InputRate = VkVertexInputRate.Vertex,
        };
        VkVertexInputAttributeDescription* attributes = stackalloc VkVertexInputAttributeDescription[3];
        attributes[0] = new VkVertexInputAttributeDescription { Location = 0, Binding = 0, Format = VkFormat.R32G32B32SFloat, Offset = default };
        attributes[1] = new VkVertexInputAttributeDescription { Location = 1, Binding = 0, Format = VkFormat.R32G32B32SFloat, Offset = (ByteSize32)12 };
        attributes[2] = new VkVertexInputAttributeDescription { Location = 2, Binding = 0, Format = VkFormat.R8G8B8A8UNorm, Offset = (ByteSize32)24 };
        var vertexInput = new VkPipelineVertexInputStateCreateInfo
        {
            VertexBindingDescriptionCount = 1,
            VertexBindingDescriptions = &binding,
            VertexAttributeDescriptionCount = 3,
            VertexAttributeDescriptions = attributes,
        };
        var inputAssembly = new VkPipelineInputAssemblyStateCreateInfo { Topology = VkPrimitiveTopology.TriangleList };
        var viewport = new VkPipelineViewportStateCreateInfo { ViewportCount = 1, ScissorCount = 1 };
        var rasterization = new VkPipelineRasterizationStateCreateInfo
        {
            PolygonMode = VkPolygonMode.Fill,
            CullMode = VkCullModeFlags.BackBit,
            FrontFace = VkFrontFace.Clockwise,
            LineWidth = 1f,
        };
        var multisample = new VkPipelineMultisampleStateCreateInfo { RasterizationSamples = VkSampleCountFlags._1Bit };

        // The projection gives the depth 0 on the near plane and 1 on the far plane. Thus the nearer fragment has the smaller depth.
        var depthStencil = new VkPipelineDepthStencilStateCreateInfo
        {
            DepthTestEnable = true,
            DepthWriteEnable = true,
            DepthCompareOp = VkCompareOp.Less,
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

    // The order of the values agrees with the constant IDs 0 to 33 in chunk.frag. All the values have 32 bits.
    private static uint[] SpecializationValues(SceneLighting lighting, bool encodeSrgb)
    {
        var values = new List<uint>(SpecializationConstantCount);
        AddFloat3(values, lighting.DirectionToLight);
        AddFloat3(values, lighting.LightColor);
        foreach (float3 coefficient in lighting.Ambient.Coefficients)
        {
            AddFloat3(values, coefficient);
        }

        values.Add(encodeSrgb ? 1u : 0u);
        return values.Count == SpecializationConstantCount
            ? [.. values]
            : throw new InvalidOperationException($"The fragment shader has {SpecializationConstantCount} specialization constants, not {values.Count}.");
    }

    private static void AddFloat3(List<uint> values, float3 value)
    {
        values.Add(BitConverter.SingleToUInt32Bits(value.X));
        values.Add(BitConverter.SingleToUInt32Bits(value.Y));
        values.Add(BitConverter.SingleToUInt32Bits(value.Z));
    }
}
