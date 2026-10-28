// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using Brutal.Logging;
using Brutal.Numerics;
using ElysianOutpost.App.Input;
using ElysianOutpost.App.Rendering;
using ElysianOutpost.Core.Cameras;
using ElysianOutpost.Core.Voxels;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.App;

/// <summary>
/// Finds the voxel at the screen center (#40), and removes and places voxels with the mouse (#41).
/// </summary>
/// <remarks>
/// <para>
/// In fly mode, the left click removes the voxel at the screen center, the right click places a voxel of the current block type
/// before it, and the mouse wheel changes the current block type. In menu mode, the cursor is free, thus the clicks and the wheel do nothing.
/// </para>
/// <para>
/// After an edit, the editor meshes the changed chunk again and replaces its GPU buffers, on the main thread.
/// Thus the change shows in the same frame. Each chunk mesh has the faces at the chunk borders, thus the neighbor chunks do not change.
/// </para>
/// </remarks>
internal sealed class VoxelEditor
{
    private const string InitialBlockName = "grass";

    private readonly VoxelEditInput _input;
    private readonly WorldBuilder _builder;
    private readonly VulkanRenderer _renderer;
    private readonly BlockPalette _palette;
    private readonly LogCategory _log;
    private readonly BlockSelector _selector;
    private readonly List<VoxelEditAction> _actions = [];
    private readonly List<int3> _changedChunks = [];
    private readonly List<BuiltChunk> _rebuiltChunks = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="VoxelEditor"/> class.
    /// </summary>
    /// <param name="input">The mouse input of the edits.</param>
    /// <param name="builder">The world builder. It has the voxels and meshes the changed chunks.</param>
    /// <param name="renderer">The renderer that receives the new chunk meshes.</param>
    /// <param name="palette">The palette. Its block types with a name are the block types that the player can place.</param>
    /// <param name="log">The log category of the edits.</param>
    public VoxelEditor(VoxelEditInput input, WorldBuilder builder, VulkanRenderer renderer, BlockPalette palette, LogCategory log)
    {
        _input = input;
        _builder = builder;
        _renderer = renderer;
        _palette = palette;
        _log = log;
        ushort[] blockTypes = palette.GetNamedSolidBlockTypes();
        ushort initial = Array.Find(blockTypes, blockType => palette.GetName(blockType) == InitialBlockName);
        _selector = new BlockSelector(blockTypes, initial != 0 ? initial : blockTypes[0]);
    }

    /// <summary>
    /// Gets the voxel at the screen center, or <c>null</c> if the camera looks at no voxel or the editor is not active.
    /// </summary>
    public VoxelRayHit? Target { get; private set; }

    /// <summary>
    /// Gets the current block type: the block type of a placed voxel.
    /// </summary>
    public ushort BlockType => _selector.Current;

    /// <summary>
    /// Gets the name of the current block type.
    /// </summary>
    public string BlockName => _palette.GetName(_selector.Current) ?? string.Empty;

    /// <summary>
    /// Finds the voxel at the screen center, and does the edits of the mouse input. Call it one time in each frame, after the camera update.
    /// </summary>
    /// <param name="camera">The camera. The ray starts at its position and goes along its forward vector, through the screen center.</param>
    /// <param name="showTarget"><c>true</c> to find the voxel at the screen center. <c>false</c> during the start screen.</param>
    /// <param name="edit"><c>true</c> in fly mode: the clicks and the wheel change the world. <c>false</c>: the editor discards the input.</param>
    public void Update(FreeFlyCamera camera, bool showTarget, bool edit)
    {
        _actions.Clear();
        double scroll = _input.Take(_actions);
        Target = showTarget ? Cast(camera) : null;
        if (!showTarget || !edit)
        {
            return;
        }

        if (_selector.Scroll(scroll))
        {
            _log.Info($"Block type: {BlockName} ({BlockType}).");
        }

        long start = Stopwatch.GetTimestamp();
        _changedChunks.Clear();
        foreach (VoxelEditAction action in _actions)
        {
            if (Target is not VoxelRayHit hit || !TryEdit(action, hit, camera.Position, out int3 chunk))
            {
                continue;
            }

            if (!_changedChunks.Contains(chunk))
            {
                _changedChunks.Add(chunk);
            }

            // The next click of the same frame uses the changed world.
            Target = Cast(camera);
        }

        if (_changedChunks.Count == 0)
        {
            return;
        }

        _rebuiltChunks.Clear();
        foreach (int3 chunk in _changedChunks)
        {
            _rebuiltChunks.Add(_builder.RebuildChunk(chunk));
        }

        long meshed = Stopwatch.GetTimestamp();
        _renderer.AddChunks(CollectionsMarshal.AsSpan(_rebuiltChunks));
        _log.Info($"{_actions.Count} click(s) changed {_changedChunks.Count} chunk(s). Meshing: {Stopwatch.GetElapsedTime(start, meshed).TotalMilliseconds:F1} ms. Upload to the GPU: {Stopwatch.GetElapsedTime(meshed).TotalMilliseconds:F1} ms.");
    }

    private VoxelRayHit? Cast(FreeFlyCamera camera) =>
        VoxelRay.TryCast(_builder.Voxels, camera.Position, camera.Forward, out VoxelRayHit hit) ? hit : null;

    private bool TryEdit(VoxelEditAction action, VoxelRayHit hit, float3 cameraPosition, out int3 chunk)
    {
        if (action == VoxelEditAction.Remove)
        {
            ushort removed = hit.BlockType;
            bool done = VoxelEdits.TryRemove(_builder.Voxels, hit, out chunk);
            if (done)
            {
                _log.Info($"Removed the voxel ({hit.Voxel.X}, {hit.Voxel.Y}, {hit.Voxel.Z}), block type {removed}.");
            }

            return done;
        }

        if (VoxelEdits.TryPlace(_builder.Voxels, hit, BlockType, cameraPosition, out chunk))
        {
            int3 placed = hit.EmptyVoxel;
            _log.Info($"Placed a voxel of block type {BlockName} ({BlockType}) at ({placed.X}, {placed.Y}, {placed.Z}).");
            return true;
        }

        _log.Info($"A voxel cannot go at ({hit.EmptyVoxel.X}, {hit.EmptyVoxel.Y}, {hit.EmptyVoxel.Z}): the position is outside the world, not empty, or in the camera.");
        return false;
    }
}
