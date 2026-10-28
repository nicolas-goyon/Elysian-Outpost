// SPDX-License-Identifier: MPL-2.0

using Brutal.GlfwApi;
using Brutal.Logging;
using Brutal.Numerics;
using ElysianOutpost.Core.Cameras;

namespace ElysianOutpost.App.Input;

/// <summary>
/// Controls the free-fly camera with the keyboard and the mouse, as the legacy scripts <c>CameraMovements</c> and <c>CameraHandler</c>.
/// </summary>
/// <remarks>
/// <para>
/// The controller reads the input by polling: in each frame, it reads the state of the keys and the position of the cursor.
/// It does not set GLFW input callbacks. Thus other parts of the app can set them, for example the debug overlay (#36).
/// </para>
/// <para>
/// The app starts in menu mode, as the legacy scene. The menu key changes the mode.
/// In fly mode, the cursor is hidden and locked, as <c>CursorLockMode.Locked</c>.
/// In menu mode, the cursor is visible and stays in the window, as <c>CursorLockMode.Confined</c>.
/// </para>
/// </remarks>
internal sealed class FreeFlyCameraController
{
    // The position of the camera in the legacy scene (SampleScene.unity). The scene gives the camera no rotation.
    private static readonly float3 _legacyStartPosition = new(0f, 47.1f, -10f);

    private readonly GlfwWindow _window;
    private readonly InputSettings _settings;
    private readonly LogCategory _log;
    private readonly CursorMovementTracker _cursorMovement = new();
    private GlfwKey _menuKey;
    private string? _menuKeyName;
    private bool _menuKeyWasDown;

    /// <summary>
    /// Initializes a new instance of the <see cref="FreeFlyCameraController"/> class, in menu mode.
    /// </summary>
    /// <param name="window">The window that receives the input.</param>
    /// <param name="settings">The input values.</param>
    /// <param name="log">The log category of the camera.</param>
    public FreeFlyCameraController(GlfwWindow window, InputSettings settings, LogCategory log)
    {
        _window = window;
        _settings = settings;
        _log = log;
        Camera = new FreeFlyCamera(settings.Camera, _legacyStartPosition);

        // The acceleration of the system cursor does not change the raw mouse motion.
        // GLFW uses the raw mouse motion only in the disabled cursor mode, thus only in fly mode.
        // The X server sends each raw motion event two times to GLFW while GLFW grabs the cursor
        // (https://gitlab.freedesktop.org/xorg/xserver/-/issues/597). Thus the camera turns two times too fast with raw motion on X11.
        // On X11, the controller uses the motion of the system cursor.
        bool rawMotion = Glfw.GetPlatform() != GlfwPlatform.X11 && Glfw.RawMotionSupported();
        if (rawMotion)
        {
            window.SetInputMode(GlfwInputMode.RawMouseMotion, Glfw.Constants.GLFW_TRUE);
        }

        log.Info($"Raw mouse motion: {(rawMotion ? "on" : "off")}.");

        // On X11, a cursor enter event and a focus event can make the virtual cursor jump with no mouse movement (#106).
        // GLFW sends these events in the same event processing as the jump. Thus the tracker ignores the movement of the next frame.
        // These events of GlfwWindow are not new GLFW callbacks: GlfwWindow sends its events to all the handlers.
        // The controller lives as long as the window. Thus it does not remove the handlers.
        window.OnCursorEnter += (_, entered) =>
        {
            if (entered)
            {
                _cursorMovement.IgnoreNextMovement();
            }
        };
        window.OnFocus += (_, _) => _cursorMovement.IgnoreNextMovement();

        FindMenuKey();
        SetMode(CameraMode.Menu);
    }

    /// <summary>
    /// Gets the camera.
    /// </summary>
    public FreeFlyCamera Camera { get; }

    /// <summary>
    /// Gets the mode of the camera controls.
    /// </summary>
    public CameraMode Mode { get; private set; }

    /// <summary>
    /// Reads the input and updates the camera. Call this method one time in each frame, after <c>Glfw.PollEvents</c>.
    /// </summary>
    /// <param name="frameTime">The time since the previous frame.</param>
    public void Update(TimeSpan frameTime)
    {
        // The keyboard layout can change at any time. Then the name of the menu key changes, and the controller finds the key again,
        // as the legacy binding #(M). One call of glfwGetKeyName in each frame is sufficient for this check.
        if (!string.Equals(Glfw.GetKeyName(_menuKey, 0), _menuKeyName, StringComparison.Ordinal))
        {
            FindMenuKey();
        }

        bool focused = IsFocused();
        bool menuKeyDown = IsDown(_menuKey);
        if (menuKeyDown && !_menuKeyWasDown)
        {
            SetMode(Mode == CameraMode.Menu ? CameraMode.Fly : CameraMode.Menu);
        }

        _menuKeyWasDown = menuKeyDown;

        // As in the legacy script, the camera does not move in menu mode.
        if (Mode != CameraMode.Fly || !focused)
        {
            _cursorMovement.Reset();
            return;
        }

        // The controller reads the cursor from GLFW, because the cached position of GlfwWindow needs its cursor callback.
        Glfw.PInvoke.GetCursorPos(_window, out double cursorX, out double cursorY);
        float2 cursorMovement = _cursorMovement.Next(new double2(cursorX, cursorY));
        if (cursorMovement.X != 0f || cursorMovement.Y != 0f)
        {
            Camera.Look(cursorMovement);
        }

        var direction = new float3(
            Axis(_settings.MoveRight, _settings.MoveLeft),
            Axis(_settings.MoveUp, _settings.MoveDown),
            Axis(_settings.MoveForward, _settings.MoveBackward));
        bool fast = false;
        foreach (GlfwKey key in _settings.SpeedKeys)
        {
            fast |= IsDown(key);
        }

        // The camera limits the duration of the movement, as Unity limits Time.deltaTime.
        Camera.Move(direction, fast, (float)frameTime.TotalSeconds);
    }

    private void SetMode(CameraMode mode)
    {
        GlfwCursorMode cursorMode = mode == CameraMode.Fly ? GlfwCursorMode.Disabled : GlfwCursorMode.Captured;
        _window.SetInputMode(GlfwInputMode.Cursor, (int)cursorMode);
        Mode = mode;

        // The first frame of fly mode only reads the cursor position. Thus the cursor movement before this frame does not turn the camera.
        _cursorMovement.Reset();

        float3 position = Camera.Position;
        _log.Info($"Camera mode: {mode}. Position: {position.X:F1}, {position.Y:F1}, {position.Z:F1}. Yaw: {Camera.Yaw:F1} degrees. Pitch: {Camera.Pitch:F1} degrees.");
    }

    // Finds the physical key that gives the menu character with the current keyboard layout, and keeps the name of this key.
    // If no key gives the menu character, the menu key is the fallback key.
    private void FindMenuKey()
    {
        GlfwKey menuKey = _settings.MenuFallbackKey;
        bool found = false;
        foreach (GlfwKey key in Enum.GetValues<GlfwKey>())
        {
            // GLFW gives a name only to the printable keys. A key that the keyboard does not have has no scancode.
            if (key < GlfwKey.Apostrophe || key > GlfwKey.World2 || Glfw.GetKeyScanCode((int)key) < 0)
            {
                continue;
            }

            if (string.Equals(Glfw.GetKeyName(key, 0), _settings.MenuCharacter, StringComparison.OrdinalIgnoreCase))
            {
                menuKey = key;
                found = true;
                break;
            }
        }

        _menuKey = menuKey;
        _menuKeyName = Glfw.GetKeyName(menuKey, 0);
        if (found)
        {
            _log.Info($"Menu key: the physical key {menuKey} gives the character \"{_settings.MenuCharacter}\".");
        }
        else
        {
            _log.Info($"Menu key: no key gives the character \"{_settings.MenuCharacter}\". The menu key is the physical key {menuKey}.");
        }
    }

    private float Axis(GlfwKey positive, GlfwKey negative) => (IsDown(positive) ? 1f : 0f) - (IsDown(negative) ? 1f : 0f);

    private bool IsDown(GlfwKey key) => _window.GetKey(key) == GlfwKeyAction.Press;

    /// <summary>
    /// Gets a value that tells if the window has the input focus.
    /// </summary>
    /// <returns><c>true</c> if the window has the focus.</returns>
    public bool IsFocused() => _window.GetAttribute(GlfwWindowAttribute.Focused) == Glfw.Constants.GLFW_TRUE;
}
