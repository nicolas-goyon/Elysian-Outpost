// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.ImPlotApi;
using Brutal.Numerics;
using ElysianOutpost.Core.Diagnostics;

namespace ElysianOutpost.App.Ui;

/// <summary>
/// The debug overlay: an ImGui window with the performance of the app. The F3 key shows and hides it.
/// </summary>
/// <remarks>
/// The overlay is visible at the start, because the app shows the performance nowhere else.
/// When the overlay is hidden, the app does not start ImGui frames.
/// Then the overlay only adds the times of each frame to its statistics, and the graph stays complete.
/// The overlay does not allocate managed memory in each frame.
/// </remarks>
internal sealed class DebugOverlay : IDisposable
{
    private const double GraphSeconds = 10;

    // At the limit of 240 frames per second of the main loop, 10 seconds have 2,400 frames.
    private const int HistoryCapacity = 4096;

    private static readonly TimeSpan _statisticsPeriod = TimeSpan.FromSeconds(0.5);
    private static readonly float2 _windowPosition = new(10, 10);
    private static readonly float2 _graphSize = new(360, 140);

    private readonly GlfwWindow _window;
    private readonly ImGuiHost _imGui;
    private readonly string _gpuName;
    private readonly FrameStatistics _statistics = new(_statisticsPeriod, HistoryCapacity);
    private readonly GlfwKeyCallback _keyCallback;
    private double _frameStartSeconds;
    private TimeSpan _buildTime;
    private bool _visible = true;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DebugOverlay"/> class.
    /// </summary>
    /// <param name="window">The window. The overlay reads the F3 key from it.</param>
    /// <param name="imGui">The ImGui host that draws the overlay.</param>
    /// <param name="gpuName">The name of the GPU that the renderer uses.</param>
    public DebugOverlay(GlfwWindow window, ImGuiHost imGui, string gpuName)
    {
        _window = window;
        _imGui = imGui;
        _gpuName = gpuName;
        _keyCallback = OnKey;
        _window.OnKey += _keyCallback;
    }

    /// <summary>
    /// Gets or sets a value that tells if the overlay is visible.
    /// </summary>
    public bool Visible
    {
        get => _visible;
        set
        {
            _visible = value;
            _imGui.InputEnabled = value;
        }
    }

    /// <summary>
    /// Adds the times of one frame to the statistics. Call it one time in each frame, before <see cref="BuildFrame"/>.
    /// </summary>
    /// <param name="frameStart">The start of the frame, from the clock of the main loop.</param>
    /// <param name="tickTime">The time of the simulation ticks in the frame.</param>
    /// <param name="overlayRecordTime">The time that the renderer used to record the ImGui commands in the previous frame.</param>
    public void AddFrame(TimeSpan frameStart, TimeSpan tickTime, TimeSpan overlayRecordTime)
    {
        // The overlay time of the previous frame: the time to build the ImGui frame and to record its commands.
        _statistics.AddFrame(frameStart, tickTime, _buildTime + overlayRecordTime);
        _frameStartSeconds = frameStart.TotalSeconds;
    }

    /// <summary>
    /// Builds the ImGui frame of the overlay.
    /// </summary>
    /// <returns>The draw data for the renderer, or a null pointer if the overlay is hidden.</returns>
    public ImDrawDataPtr BuildFrame()
    {
        if (!_visible)
        {
            _buildTime = TimeSpan.Zero;
            return default;
        }

        long start = Stopwatch.GetTimestamp();
        _imGui.NewFrame();
        DrawPerformanceWindow();
        ImDrawDataPtr drawData = _imGui.Render();
        _buildTime = Stopwatch.GetElapsedTime(start);
        return drawData;
    }

    /// <summary>
    /// Stops the reading of the F3 key.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.OnKey -= _keyCallback;
    }

    private void OnKey(GlfwWindow window, GlfwKey key, int scancode, GlfwKeyAction action, GlfwModifier mods)
    {
        if (key == GlfwKey.F3 && action == GlfwKeyAction.Press)
        {
            Visible = !Visible;
        }
    }

    private void DrawPerformanceWindow()
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
            DrawFrameTimeGraph();
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
