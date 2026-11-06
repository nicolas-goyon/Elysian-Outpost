// SPDX-License-Identifier: MPL-2.0

using Brutal.GlfwApi;
using ElysianOutpost.Core.Cameras;

namespace ElysianOutpost.App.Input;

/// <summary>
/// Gives all the input values of the app: the keys and the control values of the camera.
/// </summary>
/// <remarks>
/// <para>
/// The movement keys, the speed keys and the console key are physical keys. GLFW names a physical key after its position on a US keyboard.
/// Thus these keys are at the same positions on all keyboard layouts. On an AZERTY keyboard, the movement keys are Z, Q, S, D, A and E.
/// </para>
/// <para>
/// The menu key is a character, as the legacy binding <c>Keyboard/#(M)</c>.
/// On an AZERTY keyboard, the menu key is the key with the letter M, at the position of the semicolon on a US keyboard.
/// </para>
/// <para>
/// The default values are the values of the legacy project. A settings file will change them later (#73).
/// The legacy project had no binding for the voxel edits. The mouse buttons of the voxel edits come from #41.
/// </para>
/// </remarks>
internal sealed record InputSettings
{
    /// <summary>
    /// Gets the default input values.
    /// </summary>
    public static InputSettings Default { get; } = new();

    /// <summary>
    /// Gets the physical key that moves the camera forward.
    /// </summary>
    public GlfwKey MoveForward { get; init; } = GlfwKey.W;

    /// <summary>
    /// Gets the physical key that moves the camera back.
    /// </summary>
    public GlfwKey MoveBackward { get; init; } = GlfwKey.S;

    /// <summary>
    /// Gets the physical key that moves the camera to the left.
    /// </summary>
    public GlfwKey MoveLeft { get; init; } = GlfwKey.A;

    /// <summary>
    /// Gets the physical key that moves the camera to the right.
    /// </summary>
    public GlfwKey MoveRight { get; init; } = GlfwKey.D;

    /// <summary>
    /// Gets the physical key that moves the camera to the top of the view.
    /// </summary>
    public GlfwKey MoveUp { get; init; } = GlfwKey.E;

    /// <summary>
    /// Gets the physical key that moves the camera to the bottom of the view.
    /// </summary>
    public GlfwKey MoveDown { get; init; } = GlfwKey.Q;

    /// <summary>
    /// Gets the physical keys that multiply the speed of the camera. One of these keys is sufficient.
    /// </summary>
    public IReadOnlyList<GlfwKey> SpeedKeys { get; init; } = [GlfwKey.LeftShift, GlfwKey.RightShift];

    /// <summary>
    /// Gets the character of the key that changes the camera mode.
    /// </summary>
    public string MenuCharacter { get; init; } = "m";

    /// <summary>
    /// Gets the physical key that changes the camera mode if no key of the keyboard layout gives <see cref="MenuCharacter"/>.
    /// </summary>
    public GlfwKey MenuFallbackKey { get; init; } = GlfwKey.M;

    /// <summary>
    /// Gets the mouse button that removes the voxel at the screen center, in fly mode (#41).
    /// </summary>
    public GlfwMouseButton RemoveVoxelButton { get; init; } = GlfwMouseButton.Left;

    /// <summary>
    /// Gets the mouse button that places a voxel before the voxel at the screen center, in fly mode (#41).
    /// </summary>
    public GlfwMouseButton PlaceVoxelButton { get; init; } = GlfwMouseButton.Right;

    /// <summary>
    /// Gets the physical key that opens and closes the in-game console (#37), as the legacy binding <c>Keyboard/rightBracket</c>.
    /// </summary>
    /// <remarks>
    /// The key is the key "]" of a US keyboard. On an AZERTY keyboard, it is the key "$".
    /// </remarks>
    public GlfwKey ConsoleKey { get; init; } = GlfwKey.RightBracket;

    /// <summary>
    /// Gets the control values of the camera: the speeds, the mouse sensitivity and the pitch limits.
    /// </summary>
    public FreeFlyCameraSettings Camera { get; init; } = new();
}
