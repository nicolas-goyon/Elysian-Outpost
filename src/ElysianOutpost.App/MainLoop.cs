// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using Brutal.GlfwApi;
using Brutal.Logging;
using ElysianOutpost.App.Input;
using ElysianOutpost.App.Rendering;
using ElysianOutpost.App.Ui;
using ElysianOutpost.Core.Diagnostics;
using ElysianOutpost.Core.Simulation;
using ElysianOutpost.Core.Voxels;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.App;

/// <summary>
/// Runs the main loop of the app: the window events, the simulation ticks and the frames.
/// </summary>
/// <param name="window">The window of the app.</param>
/// <param name="renderer">The renderer that draws the frames in the window.</param>
/// <param name="ui">The ImGui parts of the app: the start screen, the debug overlay and the credits window.</param>
/// <param name="log">The log category of the app.</param>
/// <param name="timeLimit">The time after which the loop closes the window, or <c>null</c> for no limit.</param>
/// <param name="profileAtStart"><c>true</c> to start a performance profile at the start of the loop (#80).</param>
internal sealed class MainLoop(GlfwWindow window, VulkanRenderer renderer, UserInterface ui, LogCategory log, TimeSpan? timeLimit, bool profileAtStart)
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

    // VMA goes through all its memory blocks for the statistics. Thus the loop calculates them only two times in each second.
    private static readonly TimeSpan _memoryPeriod = TimeSpan.FromSeconds(0.5);


    /// <summary>
    /// Runs the loop until the window must close.
    /// </summary>
    public void Run()
    {
        var clock = new FixedStepClock(TicksPerSecond, MaxTicksPerFrame);
        var cameraController = new FreeFlyCameraController(window, InputSettings.Default, LogCategory.Make("Camera"));
        LogCategory worldLog = LogCategory.Make("World");
        using var streamer = new ChunkStreamer(
            WorldSettings.SampleScene,
            BlockPalette.Game,
            onError: (job, exception) => worldLog.Error($"{job} failed: {exception}"));
        ChunkUploadSettings uploadSettings = ChunkUploadSettings.Default;
        var uploader = new ChunkUploader(renderer, WorldSettings.SampleScene.ChunkSize, uploadSettings);
        var world = new WorldLoader(streamer, WorldSettings.SampleScene.GetChunkPositions(default), uploader, renderer, uploadSettings, worldLog);
        using var editInput = new VoxelEditInput(window, InputSettings.Default);
        var editor = new VoxelEditor(editInput, streamer, uploader, renderer, BlockPalette.Game, LogCategory.Make("Edit"));
        var profiler = new PerformanceProfiler(
            Path.Combine(AppContext.BaseDirectory, "profiles"),
            () => HardwareReports.Create(window, renderer, SystemInfo.Collect()),
            LogCategory.Make("Profile"));
        ui.Overlay.HardwareReport = HardwareReports.Create(window, renderer, SystemInfo.Collect());
        ui.Console.RegisterCommand(profiler.ConsoleCommand);
        if (profileAtStart)
        {
            profiler.Start();
        }

        long gpuBytes = 0;
        var stopwatch = Stopwatch.StartNew();
        TimeSpan previousFrameStart = TimeSpan.Zero;
        TimeSpan reportStart = TimeSpan.Zero;
        TimeSpan? memoryUpdate = null;
        long reportTickCount = 0;
        long reportDroppedTickCount = 0;
        int reportFrameCount = 0;

        log.Info($"The main loop starts: {TicksPerSecond} ticks per second, a maximum of {MaxTicksPerFrame} ticks in one frame.");
        while (true)
        {
            TimeSpan frameStart = stopwatch.Elapsed;
            long frameStartTimestamp = Stopwatch.GetTimestamp();

            // Glfw.PollEvents also throws the exceptions of the window callbacks.
            // The callbacks can also run in Glfw.WaitEventsTimeout, which does not throw their exceptions.
            // Thus the loop calls Glfw.PollEvents before it examines ShouldClose.
            Glfw.PollEvents();
            if (window.ShouldClose)
            {
                break;
            }

            // During the start screen, the camera input does nothing (BRUTAL License v1.0, section 2.2).
            long updateStart = Stopwatch.GetTimestamp();
            ui.Update(frameStart);
            bool startScreen = ui.StartScreen.IsActive;
            if (!startScreen)
            {
                cameraController.Update(frameStart - previousFrameStart, ui.KeyboardCaptured);
            }

            // The voxel edits change the world only in fly mode, because the cursor is free in menu mode.
            // Without the focus, a click can only give the focus back to the window. Thus it does not change the world.
            editor.Update(
                cameraController.Camera,
                showTarget: !startScreen,
                edit: cameraController.Mode == CameraMode.Fly && cameraController.IsFocused());
            ui.Overlay.Target = editor.Target;
            ui.Overlay.BlockType = editor.BlockType;
            ui.Overlay.BlockName = editor.BlockName;
            ui.Overlay.DrawnChunkCount = renderer.DrawnChunkCount;
            ui.Overlay.ChunkCount = renderer.ChunkCount;
            if (memoryUpdate is not TimeSpan last || frameStart - last >= _memoryPeriod)
            {
                GpuMemoryStatistics frameMemory = renderer.CalculateMemoryStatistics();
                ui.Overlay.GpuMemory = frameMemory;
                gpuBytes = frameMemory.AllocationBytes;
                memoryUpdate = frameStart;
            }

            int ticks = clock.Advance(frameStart - previousFrameStart);
            previousFrameStart = frameStart;
            long ticksStart = Stopwatch.GetTimestamp();
            for (int i = 0; i < ticks; i++)
            {
                Tick();
            }

            long ticksEnd = Stopwatch.GetTimestamp();
            ui.AddFrame(frameStart, Stopwatch.GetElapsedTime(ticksStart, ticksEnd), renderer.ImGuiRecordTime);

            // The world builds also during the start screen, but it shows only after the start screen.
            long worldStart = Stopwatch.GetTimestamp();
            world.Update(cameraController.Camera.Position);
            ui.Overlay.UploadQueueLength = uploader.QueueLength;
            ui.Overlay.UploadBudget = uploader.Budget;
            long renderStart = Stopwatch.GetTimestamp();
            bool presented = renderer.DrawFrame(cameraController.Camera.CreateViewMatrix(), ui.BuildFrame(), showScene: !startScreen, editor.Target?.Voxel);
            long renderEnd = Stopwatch.GetTimestamp();
            if (profiler.IsRecording)
            {
                profiler.Record(frameStartTimestamp, new FrameSample
                {
                    EventsMilliseconds = Stopwatch.GetElapsedTime(frameStartTimestamp, updateStart).TotalMilliseconds,
                    UpdateMilliseconds = Stopwatch.GetElapsedTime(updateStart, ticksStart).TotalMilliseconds,
                    TickMilliseconds = Stopwatch.GetElapsedTime(ticksStart, ticksEnd).TotalMilliseconds,
                    TickCount = ticks,
                    WorldMilliseconds = Stopwatch.GetElapsedTime(worldStart, renderStart).TotalMilliseconds,
                    RenderMilliseconds = Stopwatch.GetElapsedTime(renderStart, renderEnd).TotalMilliseconds,
                    GpuMilliseconds = renderer.LastGpuFrameMilliseconds,
                    ManagedBytes = GC.GetTotalMemory(false),
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2),
                    GpuBytes = gpuBytes,
                    ChunkQueueLength = uploader.QueueLength,
                });
                profiler.AddCost(Stopwatch.GetTimestamp() - renderEnd);
            }
            if (presented)
            {
                reportFrameCount++;
                ui.OnFramePresented(stopwatch.Elapsed);
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

        if (profiler.IsRecording)
        {
            profiler.Stop();
        }

        world.LogIfIncomplete();
        GpuMemoryStatistics memory = renderer.CalculateMemoryStatistics();
        log.Info($"GPU memory at the end: {memory.AllocationCount} VMA allocations, {memory.AllocationBytes} bytes. Chunk meshes: {memory.ChunkBufferCount} buffers, {memory.ChunkBufferBytes} bytes in blocks of {memory.ChunkBlockBytes} bytes.");
        log.Info($"The main loop stops after {clock.TickCount} ticks.");
    }

    // One step of the simulation. The simulation has no content yet.
    private static void Tick()
    {
    }
}
