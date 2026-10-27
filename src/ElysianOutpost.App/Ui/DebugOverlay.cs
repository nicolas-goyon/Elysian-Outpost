// SPDX-License-Identifier: MPL-2.0

using Brutal.ImGuiApi;
using Brutal.ImPlotApi;
using Brutal.Numerics;
using ElysianOutpost.Core.Diagnostics;
using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.App.Ui;

/// <summary>
/// The debug overlay: an ImGui window with the performance of the app. The F3 key shows and hides it (see <see cref="UserInterface"/>).
/// </summary>
/// <remarks>
/// The overlay is visible after the start screen, because the app shows the performance nowhere else.
/// When the overlay is hidden, it only adds the times of each frame to its statistics, and the graph stays complete.
/// The overlay does not allocate managed memory in each frame.
/// </remarks>
internal sealed class DebugOverlay
{
    private const double GraphSeconds = 10;

    // At the limit of 240 frames per second of the main loop, 10 seconds have 2,400 frames.
    private const int HistoryCapacity = 4096;

    private static readonly TimeSpan _statisticsPeriod = TimeSpan.FromSeconds(0.5);
    private static readonly float2 _windowPosition = new(10, 10);
    private static readonly float2 _graphSize = new(360, 140);

    private readonly string _gpuName;
    private readonly CreditsWindow _credits;
    private readonly FrameStatistics _statistics = new(_statisticsPeriod, HistoryCapacity);
    private double _frameStartSeconds;

    /// <summary>
    /// Initializes a new instance of the <see cref="DebugOverlay"/> class.
    /// </summary>
    /// <param name="gpuName">The name of the GPU that the renderer uses.</param>
    /// <param name="credits">The credits window. The "Credits" button of the overlay opens it.</param>
    public DebugOverlay(string gpuName, CreditsWindow credits)
    {
        _gpuName = gpuName;
        _credits = credits;
    }

    /// <summary>
    /// Gets or sets a value that tells if the overlay is visible.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Gets or sets the voxel at the center of the screen (#40), or <c>null</c> if the camera looks at no voxel. The overlay shows it.
    /// </summary>
    public VoxelRayHit? Target { get; set; }

    /// <summary>
    /// Adds the times of one frame to the statistics. Call it one time in each frame, before <see cref="Draw"/>.
    /// </summary>
    /// <param name="frameStart">The start of the frame, from the clock of the main loop.</param>
    /// <param name="tickTime">The time of the simulation ticks in the frame.</param>
    /// <param name="overlayTime">The ImGui time of the previous frame: the time to build the ImGui frame and to record its commands.</param>
    public void AddFrame(TimeSpan frameStart, TimeSpan tickTime, TimeSpan overlayTime)
    {
        _statistics.AddFrame(frameStart, tickTime, overlayTime);
        _frameStartSeconds = frameStart.TotalSeconds;
    }

    /// <summary>
    /// Draws the "Performance" window in the current ImGui frame. The close button of the window hides the overlay, as the F3 key does.
    /// </summary>
    public void Draw()
    {
        // The ImGui window stays in the top left corner. When the app window becomes very small, for example 1 x 1 pixels,
        // ImGui moves the ImGui window. The fixed position puts it back in the corner after such a change.
        ImGui.SetNextWindowPos(in _windowPosition, ImGuiCond.Always);
        bool open = true;
        // Without NoCollapse, a collapsed window stays collapsed after F3, and F3 seems to do nothing.
        const ImGuiWindowFlags Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;
        if (ImGui.Begin("Performance"u8, ref open, Flags))
        {
            if (_statistics.HasValues)
            {
                ImGui.Text($"Frames per second: {_statistics.FramesPerSecond:F1}");
                ImGui.Text($"Frame time: {_statistics.AverageFrameMilliseconds:F2} ms (maximum {_statistics.MaxFrameMilliseconds:F2} ms)");
                ImGui.Text($"Tick time: {_statistics.AverageTickMilliseconds:F3} ms");
                ImGui.Text($"Overlay time: {_statistics.AverageOverlayMilliseconds:F3} ms (main thread)");
            }
            else
            {
                ImGui.Text("Measuring..."u8);
            }

            ImGui.Text($"GPU: {_gpuName}");
            if (Target is VoxelRayHit target)
            {
                ImGui.Text($"Target: ({target.Voxel.X}, {target.Voxel.Y}, {target.Voxel.Z}), block {target.BlockType}, face {FaceName(target.Face)}, distance {target.Distance:F1}");
            }
            else
            {
                ImGui.Text("Target: none"u8);
            }

            DrawFrameTimeGraph();
            if (ImGui.Button("Credits (F1)"u8))
            {
                _credits.Open = true;
            }

            ImGui.TextDisabled("F3: show or hide this window"u8);
        }

        // ImGui needs `End` also when `Begin` returns false, for example for a collapsed window.
        ImGui.End();

        // The close button of the window hides the overlay, as the F3 key does.
        if (!open)
        {
            Visible = false;
        }
    }

    // The names of the faces are constant strings. Thus the text of the overlay does not format an enumeration in each frame.
    private static string FaceName(VoxelFace face) => face switch
    {
        VoxelFace.Xpos => "+X",
        VoxelFace.Xneg => "-X",
        VoxelFace.Ypos => "+Y (top)",
        VoxelFace.Yneg => "-Y (bottom)",
        VoxelFace.Zpos => "+Z",
        VoxelFace.Zneg => "-Z",
        _ => "none (inside)",
    };

    private void DrawFrameTimeGraph()
    {
        SampleWindow samples = _statistics.FrameTimes.GetWindow(_frameStartSeconds - GraphSeconds);

        // The vertical axis starts at 0 and shows the longest frame of the graph with a margin.
        double maxMilliseconds = Math.Max(1, samples.MaxValue() * 1.25);
        if (ImPlot.BeginPlot("##FrameTime"u8, _graphSize, ImPlotFlags.NoInputs | ImPlotFlags.NoLegend))
        {
            ImPlot.SetupAxes($"Frame time, last {GraphSeconds:F0} s", "ms"u8, ImPlotAxisFlags.NoTickLabels, ImPlotAxisFlags.None);
            ImPlot.SetupAxisLimits(ImAxis.X1, _frameStartSeconds - GraphSeconds, _frameStartSeconds, ImPlotCond.Always);
            ImPlot.SetupAxisLimits(ImAxis.Y1, 0, maxMilliseconds, ImPlotCond.Always);

            // The line uses the color of the ImGui style. The renderer converts the style colors for an sRGB swapchain.
            float4? lineColor = ImGui.GetStyle().Colors[(int)ImGuiCol.PlotLines];
            ImPlot.SetNextLineStyle(in lineColor, 1);
            ImPlot.PlotLine("Frame time"u8, samples.Times, samples.Values);
            ImPlot.EndPlot();
        }
    }
}
