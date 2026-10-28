// SPDX-License-Identifier: MPL-2.0

using System.Reflection;
using System.Text;
using Brutal;
using Brutal.ShaderCApi;
using Brutal.VulkanApi;

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// Compiles the GLSL shaders of the app to SPIR-V with shaderc (<c>Ahwoo.BRUTAL.ShaderC</c>), and makes the Vulkan shader modules.
/// </summary>
/// <remarks>
/// <para>
/// The app contains the GLSL sources as embedded resources (folder <c>Shaders</c>).
/// The app compiles them at the start, thus a change of a shader needs no other tool.
/// </para>
/// <para>
/// The first compilation of a shaderc compiler takes approximately 100 ms, and the next compilations take a few milliseconds.
/// Thus use one instance of this class for all the shaders of the renderer.
/// </para>
/// </remarks>
internal sealed unsafe class ShaderCompiler : IDisposable
{
    private const string ResourcePrefix = "ElysianOutpost.App.Shaders.";

    private readonly Compiler _compiler;
    private readonly CompileOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShaderCompiler"/> class, for Vulkan 1.3.
    /// </summary>
    public ShaderCompiler()
    {
        _compiler = ShaderC.CreateCompiler();
        _options = ShaderC.CreateCompilerOptions();
        _options.SetTargetEnv(TargetEnv.Vulkan, (uint)EnvVersion.Vulkan_1_3);
        _options.SetOptimizationLevel(OptimizationLevel.Performance);
        _options.SetWarningsAsErrors();
    }

    /// <summary>
    /// Compiles an embedded GLSL shader and makes its Vulkan shader module.
    /// </summary>
    /// <param name="device">The logical device.</param>
    /// <param name="fileName">The file name of the shader in the folder <c>Shaders</c>, for example <c>chunk.vert</c>.</param>
    /// <param name="kind">The shader stage.</param>
    /// <returns>The shader module. Release it after the creation of the pipeline.</returns>
    /// <exception cref="InvalidOperationException">The app has no such shader, or the compilation failed. The message gives the errors of shaderc.</exception>
    public VkShaderModule CreateModule(Device device, string fileName, ShaderKind kind)
    {
        byte[] spirv = Compile(fileName, kind);
        fixed (byte* code = spirv)
        {
            var info = new VkShaderModuleCreateInfo
            {
                CodeSize = (ByteSize64)spirv.Length,
                Code = (int*)code,
            };
            return device.CreateShaderModule(in info, null);
        }
    }

    /// <summary>
    /// Releases the compiler and its options.
    /// </summary>
    public void Dispose()
    {
        _options.Dispose();
        _compiler.Dispose();
    }

    private byte[] Compile(string fileName, ShaderKind kind)
    {
        byte[] source = ReadSource(fileName);

        // shaderc reads the file name and the entry point as C strings, thus they end with a zero byte.
        // A UTF-8 literal ("main"u8) has this zero byte after its last character.
        byte[] name = Encoding.UTF8.GetBytes(fileName + "\0");
        try
        {
            using CompilationResult result = _compiler.Compile(source, kind, name, "main"u8, _options);
            return new ReadOnlySpan<byte>(result.GetBytes(), checked((int)(long)result.GetLength())).ToArray();
        }
        catch (ShaderException exception)
        {
            throw new InvalidOperationException($"The shader {fileName} did not compile: {exception.Message}", exception);
        }
    }

    private static byte[] ReadSource(string fileName)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + fileName)
            ?? throw new InvalidOperationException($"The app has no embedded shader {fileName}.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
