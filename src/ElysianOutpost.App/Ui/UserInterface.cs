// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.Logging;
using Brutal.StbApi;
using ElysianOutpost.App.Rendering;

namespace ElysianOutpost.App.Ui;

/// <summary>
/// The ImGui parts of the app: the start screen, the debug overlay and the credits window. This class builds their ImGui frames.
/// </summary>
/// <remarks>
/// <para>
/// During the start screen, the ImGui frame has only the logo, and the keys and the mouse do nothing.
/// After the start screen, F3 shows and hides the debug overlay, and F1 shows and hides the credits window.
/// </para>
/// <para>
/// When no ImGui part is visible, the app does not start ImGui frames, and ImGui does not receive the input events.
/// </para>
/// </remarks>
internal sealed class UserInterface : IDisposable
{
    private const string LogoResourceName = "ElysianOutpost.App.Assets.brutal-logo.png";

    private readonly GlfwWindow _window;
    private readonly ImGuiHost _imGui;
    private readonly GlfwKeyCallback _keyCallback;
    private TimeSpan _buildTime;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserInterface"/> class. It loads the BRUTAL logo to the GPU.
    /// </summary>
    /// <param name="window">The window. The class reads the F1 and F3 keys from it.</param>
    /// <param name="imGui">The ImGui host that starts and ends the ImGui frames.</param>
    /// <param name="renderer">The renderer. It keeps the texture of the logo.</param>
    /// <param name="log">The log category of the user interface.</param>
    /// <exception cref="InvalidDataException">The logo resource is missing, or stb_image cannot decode it.</exception>
    public UserInterface(GlfwWindow window, ImGuiHost imGui, VulkanRenderer renderer, LogCategory log)
    {
        _window = window;
        _imGui = imGui;
        StartScreen = new StartScreen(LoadLogo(renderer), log);
        Credits = new CreditsWindow();
        Overlay = new DebugOverlay(renderer.GpuName, Credits);
        _keyCallback = OnKey;
        _window.OnKey += _keyCallback;
    }

    /// <summary>
    /// Gets the start screen with the BRUTAL logo.
    /// </summary>
    public StartScreen StartScreen { get; }

    /// <summary>
    /// Gets the debug overlay.
    /// </summary>
    public DebugOverlay Overlay { get; }

    /// <summary>
    /// Gets the credits window.
    /// </summary>
    public CreditsWindow Credits { get; }

    /// <summary>
    /// Ends the start screen after its minimum time. Call it at the start of each frame, before the input of the camera.
    /// </summary>
    /// <param name="frameStart">The start of the frame, from the clock of the main loop.</param>
    public void Update(TimeSpan frameStart) => StartScreen.Update(frameStart);

    /// <summary>
    /// Adds the times of one frame to the statistics of the overlay. Call it one time in each frame, before <see cref="BuildFrame"/>.
    /// </summary>
    /// <param name="frameStart">The start of the frame, from the clock of the main loop.</param>
    /// <param name="tickTime">The time of the simulation ticks in the frame.</param>
    /// <param name="imGuiRecordTime">The time that the renderer used to record the ImGui commands in the previous frame.</param>
    public void AddFrame(TimeSpan frameStart, TimeSpan tickTime, TimeSpan imGuiRecordTime) =>
        Overlay.AddFrame(frameStart, tickTime, _buildTime + imGuiRecordTime);

    /// <summary>
    /// Builds the ImGui frame of the visible parts.
    /// </summary>
    /// <returns>The draw data for the renderer, or a null pointer if no part is visible.</returns>
    public ImDrawDataPtr BuildFrame()
    {
        bool startScreen = StartScreen.IsActive;
        bool windows = !startScreen && (Overlay.Visible || Credits.Open);

        // The start screen has no ImGui window. Thus ImGui receives the input events only for the overlay and the credits.
        _imGui.InputEnabled = windows;
        if (!startScreen && !windows)
        {
            _buildTime = TimeSpan.Zero;
            return default;
        }

        long start = Stopwatch.GetTimestamp();
        _imGui.NewFrame();
        if (startScreen)
        {
            StartScreen.Draw();
        }
        else
        {
            if (Overlay.Visible)
            {
                Overlay.Draw();
            }

            Credits.Draw();
        }

        ImDrawDataPtr drawData = _imGui.Render();
        _buildTime = Stopwatch.GetElapsedTime(start);
        return drawData;
    }

    /// <summary>
    /// Records that the renderer presented a frame. Call it after each presented frame.
    /// </summary>
    /// <param name="presentTime">The time after the presentation, from the clock of the main loop.</param>
    public void OnFramePresented(TimeSpan presentTime) => StartScreen.OnFramePresented(presentTime);

    /// <summary>
    /// Stops the reading of the keys.
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

    // Decodes the PNG file of the logo with stb_image, and copies the pixels to the GPU.
    private static ImGuiTexture LoadLogo(VulkanRenderer renderer)
    {
        using Stream stream = typeof(UserInterface).Assembly.GetManifestResourceStream(LogoResourceName)
            ?? throw new InvalidDataException($"The app has no resource {LogoResourceName}.");
        byte[] file = new byte[stream.Length];
        stream.ReadExactly(file);

        // Four channels: red, green, blue and alpha, in rows from the top row.
        using StbImage8 image = Stb.LoadFromMemory(file, 4);
        if (image.IsNull())
        {
            throw new InvalidDataException($"stb_image cannot decode the resource {LogoResourceName}.");
        }

        return renderer.CreateImGuiTexture(image.AsSpan(), image.Width, image.Height);
    }

    private void OnKey(GlfwWindow window, GlfwKey key, int scancode, GlfwKeyAction action, GlfwModifier mods)
    {
        // During the start screen, the keys do nothing. A key repeat does nothing.
        if (action != GlfwKeyAction.Press || StartScreen.IsActive)
        {
            return;
        }

        if (key == GlfwKey.F3)
        {
            Overlay.Visible = !Overlay.Visible;
        }
        else if (key == GlfwKey.F1)
        {
            Credits.Open = !Credits.Open;
        }
    }
}
