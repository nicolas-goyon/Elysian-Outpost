// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Diagnostics;

/// <summary>
/// The performance values of one frame in a performance profile (#80). All times are in milliseconds.
/// </summary>
/// <remarks>
/// The CPU parts are the parts of the main loop, in their order. <see cref="FrameMilliseconds"/> also contains the wait for the next frame,
/// thus it is larger than the sum of the parts.
/// </remarks>
public readonly record struct FrameSample
{
    /// <summary>Gets the time of the start of the frame, from the start of the profile.</summary>
    public double TimeMilliseconds { get; init; }

    /// <summary>Gets the time from the start of this frame to the start of the next frame.</summary>
    public double FrameMilliseconds { get; init; }

    /// <summary>Gets the time of the window events (GLFW).</summary>
    public double EventsMilliseconds { get; init; }

    /// <summary>Gets the time of the user interface update, the camera and the voxel editor.</summary>
    public double UpdateMilliseconds { get; init; }

    /// <summary>Gets the time of the simulation ticks of the frame.</summary>
    public double TickMilliseconds { get; init; }

    /// <summary>Gets the number of simulation ticks of the frame.</summary>
    public int TickCount { get; init; }

    /// <summary>Gets the time of the chunk work of the main thread: the worker results and the GPU uploads.</summary>
    public double WorldMilliseconds { get; init; }

    /// <summary>Gets the CPU time of the frame in the renderer: the ImGui frame, the wait for a free frame, the commands and the presentation.</summary>
    public double RenderMilliseconds { get; init; }

    /// <summary>Gets the GPU time of the last frame that the GPU completed, or a negative value if it is not known.</summary>
    public double GpuMilliseconds { get; init; }

    /// <summary>Gets the managed memory of the process after the frame, in bytes.</summary>
    public long ManagedBytes { get; init; }

    /// <summary>Gets the total number of garbage collections of generation 0 since the start of the process.</summary>
    public int Gen0Collections { get; init; }

    /// <summary>Gets the total number of garbage collections of generation 1 since the start of the process.</summary>
    public int Gen1Collections { get; init; }

    /// <summary>Gets the total number of garbage collections of generation 2 since the start of the process.</summary>
    public int Gen2Collections { get; init; }

    /// <summary>Gets the GPU memory of the VMA allocations, in bytes. The app measures it two times in each second.</summary>
    public long GpuBytes { get; init; }

    /// <summary>Gets the number of chunk meshes that wait for the GPU upload.</summary>
    public int ChunkQueueLength { get; init; }
}
