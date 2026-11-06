// SPDX-License-Identifier: MPL-2.0

using Brutal.GlfwApi;
using Brutal.Numerics;

namespace ElysianOutpost.App.Input;

/// <summary>
/// Collects the mouse input of the voxel edits (#41): the clicks that remove and place voxels, and the mouse wheel that changes the block type.
/// </summary>
/// <remarks>
/// <para>
/// The class uses the GLFW callbacks, not the polled state of the buttons. A click can start and end between two frames,
/// and the polled state then does not show it. The callbacks run in <c>Glfw.PollEvents</c>, on the main thread.
/// </para>
/// <para>
/// The main loop calls <see cref="Take"/> one time in each frame. It uses the input only in fly mode, and discards it in the other cases.
/// </para>
/// </remarks>
internal sealed class VoxelEditInput : IDisposable
{
    private readonly GlfwWindow _window;
    private readonly InputSettings _settings;
    private readonly GlfwMouseButtonCallback _mouseButtonCallback;
    private readonly GlfwScrollCallback _scrollCallback;
    private readonly Queue<VoxelEditAction> _actions = new();
    private double _scroll;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoxelEditInput"/> class.
    /// </summary>
    /// <param name="window">The window. The class adds a mouse button callback and a scroll callback to it.</param>
    /// <param name="settings">The input values that give the mouse buttons.</param>
    public VoxelEditInput(GlfwWindow window, InputSettings settings)
    {
        _window = window;
        _settings = settings;
        _mouseButtonCallback = OnMouseButton;
        _scrollCallback = OnScroll;
        _window.OnMouseButton += _mouseButtonCallback;
        _window.OnScroll += _scrollCallback;
    }

    /// <summary>
    /// Gives the clicks since the previous call, in their order, and the sum of the vertical scroll offsets. Then clears them.
    /// </summary>
    /// <param name="actions">The list that receives the clicks. The method adds to the list and does not clear it.</param>
    /// <returns>The sum of the vertical scroll offsets.</returns>
    public double Take(List<VoxelEditAction> actions)
    {
        while (_actions.TryDequeue(out VoxelEditAction action))
        {
            actions.Add(action);
        }

        double scroll = _scroll;
        _scroll = 0;
        return scroll;
    }

    /// <summary>
    /// Removes the callbacks from the window.
    /// </summary>
    public void Dispose()
    {
        _window.OnMouseButton -= _mouseButtonCallback;
        _window.OnScroll -= _scrollCallback;
    }

    private void OnMouseButton(GlfwWindow window, GlfwMouseButton button, GlfwButtonAction action, GlfwModifier mods)
    {
        if (action != GlfwButtonAction.Press)
        {
            return;
        }

        if (button == _settings.RemoveVoxelButton)
        {
            _actions.Enqueue(VoxelEditAction.Remove);
        }
        else if (button == _settings.PlaceVoxelButton)
        {
            _actions.Enqueue(VoxelEditAction.Place);
        }
    }

    private void OnScroll(GlfwWindow window, double2 offset) => _scroll += offset.Y;
}

/// <summary>
/// A voxel edit of the player.
/// </summary>
internal enum VoxelEditAction
{
    /// <summary>
    /// Remove the voxel at the screen center.
    /// </summary>
    Remove,

    /// <summary>
    /// Place a voxel of the current block type before the voxel at the screen center.
    /// </summary>
    Place,
}
