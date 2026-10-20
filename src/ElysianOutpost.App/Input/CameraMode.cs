// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.App.Input;

/// <summary>
/// The modes of the camera controls.
/// </summary>
internal enum CameraMode
{
    /// <summary>
    /// The camera does not move. The cursor is visible and stays in the window.
    /// </summary>
    Menu,

    /// <summary>
    /// The keyboard moves the camera and the mouse turns it. The cursor is hidden and locked.
    /// </summary>
    Fly,
}
