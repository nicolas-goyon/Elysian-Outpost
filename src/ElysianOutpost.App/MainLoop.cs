// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using Brutal.GlfwApi;
using Brutal.Logging;
using ElysianOutpost.Core.Simulation;

namespace ElysianOutpost.App;

/// <summary>
/// Runs the main loop of the app: the window events, the simulation ticks and the frames.
/// </summary>
/// <param name="window">The window of the app.</param>
/// <param name="log">The log category of the app.</param>
/// <param name="timeLimit">The time after which the loop closes the window, or <c>null</c> for no limit.</param>
internal sealed class MainLoop(GlfwWindow window, LogCategory log, TimeSpan? timeLimit)
{
    private const int TicksPerSecond = 20;
    private const int MaxTicksPerFrame = 5;

    // Until the renderer controls the frame rate (#35), the loop waits between frames. This keeps the CPU load low.
    private const int MaxFramesPerSecond = 60;

    private static readonly TimeSpan _minFrameDuration = TimeSpan.FromSeconds(1.0 / MaxFramesPerSecond);
    private static readonly TimeSpan _reportPeriod = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Runs the loop until the window must close.
    /// </summary>
    public void Run()
    {
        var clock = new FixedStepClock(TicksPerSecond, MaxTicksPerFrame);
        var stopwatch = Stopwatch.StartNew();
        TimeSpan previousFrameStart = TimeSpan.Zero;
        TimeSpan reportStart = TimeSpan.Zero;
        long reportTickCount = 0;
        long reportDroppedTickCount = 0;
        int reportFrameCount = 0;

        log.Info($"The main loop starts: {TicksPerSecond} ticks per second, a maximum of {MaxTicksPerFrame} ticks in one frame.");
        while (!window.ShouldClose)
        {
            TimeSpan frameStart = stopwatch.Elapsed;

            // Glfw.PollEvents also throws the exceptions of the window callbacks.
            Glfw.PollEvents();

            int ticks = clock.Advance(frameStart - previousFrameStart);
            previousFrameStart = frameStart;
            for (int i = 0; i < ticks; i++)
            {
                Tick();
            }

            Draw();
            reportFrameCount++;

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

            TimeSpan wait = _minFrameDuration - (stopwatch.Elapsed - frameStart);
            if (wait > TimeSpan.Zero)
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

    // One frame. The renderer (#35) will draw the frames.
    private static void Draw()
    {
    }
}
