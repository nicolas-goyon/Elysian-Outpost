// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using Brutal.GlfwApi;
using Brutal.Logging;
using ElysianOutpost.App.Input;
using ElysianOutpost.App.Rendering;
using ElysianOutpost.Core.Simulation;

namespace ElysianOutpost.App;

/// <summary>
/// Runs the main loop of the app: the window events, the simulation ticks and the frames.
/// </summary>
/// <param name="window">The window of the app.</param>
/// <param name="renderer">The renderer that draws the frames in the window.</param>
/// <param name="log">The log category of the app.</param>
/// <param name="timeLimit">The time after which the loop closes the window, or <c>null</c> for no limit.</param>
internal sealed class MainLoop(GlfwWindow window, VulkanRenderer renderer, LogCategory log, TimeSpan? timeLimit)
{
    private const int TicksPerSecond = 20;
    private const int MaxTicksPerFrame = 5;

    // The FIFO presentation of the renderer usually limits the frame rate to the refresh rate of the screen.
    // Some systems do not wait in FIFO mode. Examples are a virtual machine, a software driver,
    // or a driver setting that turns off the vertical synchronization. Thus the loop also keeps a minimum frame duration.
    private const int MaxFramesPerSecond = 240;

    // If the window has no visible area, the renderer does not present, and the loop waits for the duration of one tick.
    private static readonly TimeSpan _minFrameDuration = TimeSpan.FromSeconds(1.0 / MaxFramesPerSecond);
    private static readonly TimeSpan _idleFrameDuration = TimeSpan.FromSeconds(1.0 / TicksPerSecond);
    private static readonly TimeSpan _reportPeriod = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Runs the loop until the window must close.
    /// </summary>
    public void Run()
    {
        var clock = new FixedStepClock(TicksPerSecond, MaxTicksPerFrame);
        var cameraController = new FreeFlyCameraController(window, InputSettings.Default, LogCategory.Make("Camera"));
        var stopwatch = Stopwatch.StartNew();
        TimeSpan previousFrameStart = TimeSpan.Zero;
        TimeSpan reportStart = TimeSpan.Zero;
        long reportTickCount = 0;
        long reportDroppedTickCount = 0;
        int reportFrameCount = 0;

        log.Info($"The main loop starts: {TicksPerSecond} ticks per second, a maximum of {MaxTicksPerFrame} ticks in one frame.");
        while (true)
        {
            TimeSpan frameStart = stopwatch.Elapsed;

            // Glfw.PollEvents also throws the exceptions of the window callbacks.
            // The callbacks can also run in Glfw.WaitEventsTimeout, which does not throw their exceptions.
            // Thus the loop calls Glfw.PollEvents before it examines ShouldClose.
            Glfw.PollEvents();
            if (window.ShouldClose)
            {
                break;
            }

            cameraController.Update(frameStart - previousFrameStart);

            int ticks = clock.Advance(frameStart - previousFrameStart);
            previousFrameStart = frameStart;
            for (int i = 0; i < ticks; i++)
            {
                Tick();
            }

            bool presented = renderer.DrawFrame();
            if (presented)
            {
                reportFrameCount++;
            }

            if (frameStart - reportStart >= _reportPeriod)
            {
                double seconds = (frameStart - reportStart).TotalSeconds;
                log.Info($"Tick rate: {(clock.TickCount - reportTickCount) / seconds:F1} per second. Frame rate: {reportFrameCount / seconds:F1} per second. Dropped ticks: {clock.DroppedTickCount - reportDroppedTickCount}.");
                reportStart = frameStart;
                reportTickCount = clock.TickCount;
                reportDroppedTickCount = clock.DroppedTickCount;
                reportFrameCount = 0;
            }

            if (timeLimit is TimeSpan limit && frameStart >= limit)
            {
                log.Info($"The time limit of {limit.TotalSeconds} seconds is reached. The app closes the window.");
                window.ShouldClose = true;
            }

            // Glfw.WaitEventsTimeout stops when an event arrives. Thus the loop waits again until the end of the frame.
            TimeSpan frameEnd = frameStart + (presented ? _minFrameDuration : _idleFrameDuration);
            for (TimeSpan wait = frameEnd - stopwatch.Elapsed;
                 wait > TimeSpan.Zero && !window.ShouldClose;
                 wait = frameEnd - stopwatch.Elapsed)
            {
                Glfw.WaitEventsTimeout(wait.TotalSeconds);
            }
        }

        log.Info($"The main loop stops after {clock.TickCount} ticks.");
    }

    // One step of the simulation. The simulation has no content yet.
    private static void Tick()
    {
    }
}
