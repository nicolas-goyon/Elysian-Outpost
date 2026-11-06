// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;

namespace ElysianOutpost.Core.Cameras;

/// <summary>
/// Calculates the cursor movement for the mouse look, from the cursor position in each frame.
/// It ignores the cursor jumps that come with some window events.
/// </summary>
/// <remarks>
/// <para>
/// In the disabled cursor mode, GLFW gives a virtual cursor position. On X11, two window events can make this position jump
/// with no mouse movement (#106):
/// </para>
/// <list type="bullet">
/// <item>
/// A cursor enter event. A smaller window or a window move can put the hidden pointer outside the window.
/// Then the X server moves the pointer into the window.
/// Just after the enter event, the <c>EnterNotify</c> handler of GLFW sets the virtual cursor to the pointer position.
/// </item>
/// <item>
/// A focus event. GLFW moves the pointer when the window loses the focus and when it gets the focus.
/// If GLFW gets the two focus events in one event processing, it adds the first move to the virtual cursor.
/// </item>
/// </list>
/// <para>
/// Thus, after such an event, the next frame gives no movement, and its cursor position becomes the new reference.
/// This frame also ignores the real mouse movement since the previous frame. On Windows and Wayland,
/// these events do not move the virtual cursor, and the rule only ignores the mouse movement of one frame.
/// </para>
/// </remarks>
public sealed class CursorMovementTracker
{
    private bool _hasReference;
    private bool _ignoreNextMovement;
    private double2 _reference;

    /// <summary>
    /// Records a window event that can move the virtual cursor: a cursor enter event or a focus event.
    /// Then the next call of <see cref="Next"/> gives no movement.
    /// </summary>
    public void IgnoreNextMovement() => _ignoreNextMovement = true;

    /// <summary>
    /// Gives the cursor movement since the previous frame, and keeps the cursor position as the reference for the next frame.
    /// </summary>
    /// <param name="cursor">The cursor position in this frame, in screen coordinates.</param>
    /// <returns>
    /// The movement in screen coordinates, with the axes of the window: X to the right and Y down.
    /// Zero in the first frame after <see cref="Reset"/>, and in the first frame after <see cref="IgnoreNextMovement"/>.
    /// </returns>
    public float2 Next(double2 cursor)
    {
        float2 movement = _hasReference && !_ignoreNextMovement
            ? new float2((float)(cursor.X - _reference.X), (float)(cursor.Y - _reference.Y))
            : new float2(0f, 0f);

        _hasReference = true;
        _ignoreNextMovement = false;
        _reference = cursor;
        return movement;
    }

    /// <summary>
    /// Forgets the reference. Call this method when the mouse look stops, for example in menu mode or without the focus.
    /// Then the next call of <see cref="Next"/> gives no movement.
    /// </summary>
    public void Reset() => _hasReference = false;
}
