// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// Measures the GPU time of each frame with two Vulkan timestamps (#80).
/// </summary>
/// <remarks>
/// <para>
/// Each frame in flight has two queries: one timestamp at the start of its command buffer, and one at the end.
/// The renderer reads the result of a frame after the wait for its fence, thus the read never waits.
/// The result is the GPU time of the frame that used the same slot, <c>FramesInFlight</c> frames before.
/// </para>
/// <para>
/// If the queue has no timestamps, <see cref="LastFrameMilliseconds"/> stays negative.
/// </para>
/// </remarks>
internal sealed class GpuTimer : IDisposable
{
    private readonly GpuContext _gpu;
    private readonly VkQueryPool _pool;
    private readonly bool[] _written;
    private readonly ulong _mask;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuTimer"/> class.
    /// </summary>
    /// <param name="gpu">The Vulkan objects of the window.</param>
    /// <param name="framesInFlight">The number of frames in flight of the renderer.</param>
    public GpuTimer(GpuContext gpu, int framesInFlight)
    {
        _gpu = gpu;
        _written = new bool[framesInFlight];
        if (gpu.TimestampValidBits == 0)
        {
            return;
        }

        _mask = gpu.TimestampValidBits >= 64 ? ulong.MaxValue : (1UL << gpu.TimestampValidBits) - 1;
        var info = new VkQueryPoolCreateInfo
        {
            QueryType = VkQueryType.Timestamp,
            QueryCount = framesInFlight * 2,
        };
        _pool = gpu.Device.CreateQueryPool(in info, null);
    }

    /// <summary>
    /// Gets the GPU time of the last frame that the GPU completed, in milliseconds, or -1 if it is not known.
    /// </summary>
    public double LastFrameMilliseconds { get; private set; } = -1;

    /// <summary>
    /// Reads the result of a frame slot. Call it after the wait for the fence of the slot, before the new commands of the slot.
    /// </summary>
    /// <param name="frame">The index of the frame in flight.</param>
    public unsafe void ReadResult(int frame)
    {
        if (_pool.IsNull() || !_written[frame])
        {
            return;
        }

        Span<ulong> values = stackalloc ulong[2];
        VkResult result = _gpu.Device.GetQueryPoolResults(
            _pool,
            frame * 2,
            2,
            MemoryMarshal.AsBytes(values),
            (Brutal.ByteSize64)sizeof(ulong),
            VkQueryResultFlags._64Bit);
        if (result == VkResult.Success)
        {
            ulong ticks = (values[1] - values[0]) & _mask;
            LastFrameMilliseconds = ticks * (double)_gpu.TimestampPeriod / 1_000_000.0;
        }
    }

    /// <summary>
    /// Records the reset of the two queries of a frame and the first timestamp. Call it at the start of the command buffer.
    /// </summary>
    /// <param name="commandBuffer">The command buffer of the frame.</param>
    /// <param name="frame">The index of the frame in flight.</param>
    public void Begin(CommandBuffer commandBuffer, int frame)
    {
        if (_pool.IsNull())
        {
            return;
        }

        commandBuffer.ResetQueryPool(_pool, frame * 2, 2);
        commandBuffer.WriteTimestamp(VkPipelineStageFlags.TopOfPipeBit, _pool, frame * 2);
    }

    /// <summary>
    /// Records the second timestamp of a frame. Call it at the end of the command buffer, after all the other commands.
    /// </summary>
    /// <param name="commandBuffer">The command buffer of the frame.</param>
    /// <param name="frame">The index of the frame in flight.</param>
    public void End(CommandBuffer commandBuffer, int frame)
    {
        if (_pool.IsNull())
        {
            return;
        }

        commandBuffer.WriteTimestamp(VkPipelineStageFlags.BottomOfPipeBit, _pool, (frame * 2) + 1);
        _written[frame] = true;
    }

    /// <summary>
    /// Releases the query pool. The GPU must not use it.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!_pool.IsNull())
        {
            _gpu.Device.DestroyQueryPool(_pool, null);
        }
    }
}
