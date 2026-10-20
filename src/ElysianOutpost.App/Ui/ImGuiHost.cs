// SPDX-License-Identifier: MPL-2.0

using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.ImGuiApi.Backends;
using Brutal.ImPlotApi;
using Brutal.Numerics;
using ElysianOutpost.App.Rendering;

namespace ElysianOutpost.App.Ui;

/// <summary>
/// The Dear ImGui and ImPlot contexts of the app, with the GLFW platform backend of ImGui.
/// </summary>
/// <remarks>
/// The renderer draws the ImGui frames with the Vulkan backend of ImGui (see <see cref="ImGuiPass"/>).
/// That backend needs the ImGui context. Thus the app creates the host before the renderer and releases it after the renderer.
/// </remarks>
internal sealed class ImGuiHost : IDisposable
{
    private readonly GlfwWindow _window;
    private readonly ImGuiContextPtr _context;
    private readonly ImPlotContextPtr _plotContext;
    private readonly GlfwBackend? _platform;
    private bool _inputEnabled = true;
    private bool _linearColors;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImGuiHost"/> class for a window.
    /// </summary>
    /// <param name="window">The window. The platform backend receives its input events.</param>
    public ImGuiHost(GlfwWindow window)
    {
        _window = window;
        _context = ImGui.CreateContext();
        _plotContext = ImPlot.CreateContext();
        try
        {
            ImPlot.SetImGuiContext(_context);

            // The app keeps no ImGui settings, thus ImGui must not write the file imgui.ini in the working folder.
            ImGui.GetIO().IniFilename = default;
            ImGui.StyleColorsDark();

            // The window background of the dark style is 94 % opaque. With an sRGB swapchain, the GPU blends in linear space.
            // Then the sky color makes the background blue-gray. An opaque background keeps the dark gray of the style.
            ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg].W = 1;
            _platform = new GlfwBackend(window, GlfwBackend.ClientApi.Vulkan);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets or sets a value that tells if ImGui receives the input events of the window.
    /// </summary>
    /// <remarks>
    /// Set it to <c>false</c> when the app does not start ImGui frames. Only <see cref="NewFrame"/> removes the events from the queue of ImGui.
    /// When the value becomes <c>true</c> again, ImGui forgets the keys and the mouse buttons that it found pressed.
    /// </remarks>
    public bool InputEnabled
    {
        get => _inputEnabled;
        set
        {
            if (value == _inputEnabled)
            {
                return;
            }

            _inputEnabled = value;
            ImGuiIOPtr io = ImGui.GetIO();
            io.SetAppAcceptingEvents(value);
            if (value)
            {
                // The release events of the keys and the buttons are lost while ImGui does not receive the events.
                io.ClearInputKeys();
                io.ClearInputMouse();
            }
            else
            {
                io.ClearEventsQueue();
            }
        }
    }

    /// <summary>
    /// Converts the colors of the ImGui style from sRGB to linear values. Call it when the swapchain has an sRGB format.
    /// The method converts the colors only one time.
    /// </summary>
    public void UseLinearColors()
    {
        if (_linearColors)
        {
            return;
        }

        _linearColors = true;
        Span<float4> colors = ImGui.GetStyle().Colors;
        foreach (ref float4 color in colors)
        {
            color = ColorSpace.SrgbToLinear(color);
        }
    }

    /// <summary>
    /// Starts an ImGui frame. Call it one time in a frame, before the ImGui windows.
    /// </summary>
    public void NewFrame()
    {
        // The ImString arguments of the ImGui functions keep their text in a shared buffer for one frame.
        ImString.SharedStorage.Reset();

        // The camera (#38) disables the cursor in fly mode and captures it in menu mode.
        // With a disabled cursor, the cursor position has no limits. Then ImGui must ignore the mouse.
        // Else a click can operate an ImGui window that the player does not see.
        int cursorMode = _window.GetInputMode(GlfwInputMode.Cursor);
        bool disabled = cursorMode == (int)GlfwCursorMode.Disabled;
        ref ImGuiConfigFlags flags = ref ImGui.GetIO().ConfigFlags;
        SetFlag(ref flags, ImGuiConfigFlags.NoMouse, disabled);

        // In each frame, the GLFW backend sets the cursor mode to normal or hidden, except for the disabled mode.
        // Thus a captured cursor becomes a normal cursor. NoMouseCursorChange stops these changes for the disabled and the captured modes.
        // The hidden mode does not get this flag, because the backend itself sets this mode.
        SetFlag(ref flags, ImGuiConfigFlags.NoMouseCursorChange, disabled || cursorMode == (int)GlfwCursorMode.Captured);

        _platform!.NewFrame();
        ImGui.NewFrame();
    }

    /// <summary>
    /// Ends the ImGui frame.
    /// </summary>
    /// <returns>The draw data for the renderer. It stays valid until the next call of <see cref="NewFrame"/>.</returns>
    public ImDrawDataPtr Render()
    {
        ImGui.Render();
        return ImGui.GetDrawData();
    }

    private static void SetFlag(ref ImGuiConfigFlags flags, ImGuiConfigFlags flag, bool value)
    {
        flags = value ? flags | flag : flags & ~flag;
    }

    /// <summary>
    /// Releases the platform backend and the contexts. Release the renderer before this call.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _platform?.Dispose();
        ImPlot.DestroyContext(_plotContext);
        ImGui.DestroyContext(_context);
    }
}
