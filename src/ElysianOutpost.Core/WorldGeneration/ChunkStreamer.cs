// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Threading.Channels;
using Brutal.Numerics;
using ElysianOutpost.Core.Meshing;
using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.WorldGeneration;

/// <summary>
/// Generates and meshes the chunks on worker threads (#47). The main thread receives the finished meshes in <see cref="Update"/>.
/// </summary>
/// <remarks>
/// <para>
/// The streamer uses <see cref="System.Threading.Channels"/>. One channel holds the jobs for the workers.
/// One channel holds the results for the main thread. The two channels are thread-safe queues.
/// </para>
/// <para>
/// The workers never touch the voxel world. A generation job returns new voxels.
/// A mesh job gets a <see cref="ChunkSnapshot"/>: an immutable copy of the voxels of the chunk and of the borders of its neighbors.
/// </para>
/// <para>
/// Each requested chunk has a state: <see cref="ChunkState.Requested"/>, <see cref="ChunkState.Generated"/> or <see cref="ChunkState.Delivered"/>.
/// A second request for the same chunk does nothing. The mesh of a chunk starts when all its requested neighbors are generated,
/// because the neighbors hide the faces at the borders (#48). When a neighbor arrives later, or when a voxel changes, the chunk gets a new mesh.
/// </para>
/// <para>
/// Each chunk also has a version number. Each change of the data of a chunk, and each new mesh job, increments it.
/// <see cref="Update"/> gives a mesh only if its version is the current version. Thus a result of old data never replaces a newer mesh.
/// A worker also skips a mesh job that is already old when the job starts.
/// </para>
/// <para>
/// The streamer does not try a failed job again. A failure of the generator or of the mesher is a defect,
/// and the same data gives the same failure. The failure goes to the error method.
/// A chunk whose generation fails leaves the streamer. A chunk whose mesh fails keeps its last mesh until the next change of its data.
/// </para>
/// <para>
/// A lock protects the chunk states. <see cref="Request"/> and <see cref="SetVoxel"/> are safe for all threads.
/// Call <see cref="Update"/> and <see cref="MeshAfterEdit"/> from one thread: the main thread.
/// Only the main thread can read or write <see cref="Voxels"/>. It can write only when no other thread calls <see cref="SetVoxel"/>.
/// </para>
/// </remarks>
public sealed class ChunkStreamer : IDisposable
{
    private readonly MainGeneration _generation;
    private readonly BlockPalette _palette;
    private readonly Action<string, Exception> _onError;
    private readonly Channel<Job> _jobs = Channel.CreateUnbounded<Job>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
    // Only the main thread reads the results. SingleReader stays false, because the single-reader channel cannot count its items.
    private readonly Channel<Result> _results = Channel.CreateUnbounded<Result>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
    private readonly Dictionary<int3, ChunkEntry> _chunks = [];
    private readonly Lock _lock = new();
    private readonly GreedyMesher _mainMesher = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread[] _workers;

    // The index in the output list of each chunk that the current call of Update or MeshAfterEdit gave. A chunk goes only one time into one list.
    private readonly Dictionary<int3, int> _outputIndex = [];
    private readonly HashSet<int3> _changedChunks = [];
    private int _pendingJobs;
    private long _generationTicks;
    private long _meshingTicks;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkStreamer"/> class, and starts the workers.
    /// </summary>
    /// <param name="settings">The settings of the world: the seed, the chunk size and the chunk layers.</param>
    /// <param name="palette">The palette that gives the colors of the block types.</param>
    /// <param name="workerCount">The number of worker threads. Default: the number of processors minus one, and at least one.</param>
    /// <param name="onError">
    /// The method that receives the exceptions of the workers, with a description of the job. A worker calls it on its own thread.
    /// Default: the exceptions go to <see cref="Trace"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> or <paramref name="palette"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="workerCount"/> is less than 1.</exception>
    public ChunkStreamer(WorldSettings settings, BlockPalette palette, int? workerCount = null, Action<string, Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(palette);
        int count = workerCount ?? Math.Max(1, Environment.ProcessorCount - 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1, nameof(workerCount));
        _generation = new MainGeneration(settings.ChunkSize, settings.Seed, settings.ChunkLayers);
        _palette = palette;
        _onError = onError ?? ((job, exception) => Trace.TraceError($"{job}: {exception}"));
        Voxels = new VoxelWorld(settings.ChunkSize);
        // Dedicated threads: the jobs are long CPU work, and the thread pool starts new threads slowly.
        _workers = new Thread[count];
        CancellationToken stop = _stop.Token;
        for (int i = 0; i < count; i++)
        {
            _workers[i] = new Thread(() => Work(stop)) { IsBackground = true, Name = $"Chunk worker {i + 1}" };
            _workers[i].Start();
        }
    }

    /// <summary>
    /// Gets the voxels of the generated chunks. See the remarks of the class for the threads that can read it.
    /// </summary>
    public VoxelWorld Voxels { get; }

    /// <summary>
    /// Gets the number of worker threads.
    /// </summary>
    public int WorkerCount => _workers.Length;

    /// <summary>
    /// Gets the number of requested chunks.
    /// </summary>
    public int RequestedCount
    {
        get
        {
            lock (_lock)
            {
                return _chunks.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of chunks that <see cref="Update"/> gave at least one time.
    /// </summary>
    public int DeliveredCount { get; private set; }

    /// <summary>
    /// Gets a value that tells if no job waits or runs, and no result waits for <see cref="Update"/>.
    /// </summary>
    public bool IsIdle => Volatile.Read(ref _pendingJobs) == 0;

    /// <summary>
    /// Gets the total time of the terrain generation, on all the workers.
    /// </summary>
    public TimeSpan GenerationTime => Stopwatch.GetElapsedTime(0, Interlocked.Read(ref _generationTicks));

    /// <summary>
    /// Gets the total time of the mesher and of the mesh data, on all the workers and on the main thread.
    /// </summary>
    public TimeSpan MeshingTime => Stopwatch.GetElapsedTime(0, Interlocked.Read(ref _meshingTicks));

    /// <summary>
    /// Gets the number of results that wait for <see cref="Update"/>. Only the tests use it.
    /// </summary>
    internal int WaitingResults => _results.Reader.Count;

    /// <summary>
    /// Gets or sets a method that a worker calls before each mesh job, with the chunk position and the version of the job.
    /// Only the tests use it: they stop a job to make its result arrive after a newer result.
    /// </summary>
    internal Action<int3, int>? MeshJobStarting { get; set; }

    /// <summary>
    /// Requests a chunk: the streamer generates it, then meshes it. A second request for the same chunk does nothing.
    /// </summary>
    /// <param name="position">The world position of the first voxel of the chunk. Each coordinate is a multiple of the chunk size.</param>
    /// <returns><c>true</c> if this is the first request for the chunk.</returns>
    /// <exception cref="ObjectDisposedException">The streamer is disposed.</exception>
    public bool Request(int3 position)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_chunks.ContainsKey(position))
            {
                return false;
            }

            _chunks.Add(position, new ChunkEntry());
            Enqueue(new Job(position, 0, null));
            return true;
        }
    }

    /// <summary>
    /// Changes a voxel, and starts a new mesh of each chunk that the voxel touches, on the workers.
    /// </summary>
    /// <param name="position">The world position of the voxel.</param>
    /// <param name="blockType">The new block type. 0 removes the voxel.</param>
    /// <returns><c>true</c> if a generated chunk contains the voxel. <c>false</c> if no chunk contains it: then nothing changes.</returns>
    /// <exception cref="ObjectDisposedException">The streamer is disposed.</exception>
    public bool SetVoxel(int3 position, ushort blockType)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!Voxels.TrySetVoxel(position, blockType, out _))
            {
                return false;
            }

            Span<int3> chunks = stackalloc int3[4];
            int count = Voxels.GetChunksToMesh(position, chunks);
            foreach (int3 chunk in chunks[..count])
            {
                _chunks[chunk].Version++;
                ScheduleMesh(chunk);
            }

            return true;
        }
    }

    /// <summary>
    /// Meshes the chunks that changed voxels touch, now, on the calling thread. Call it after the voxel edits of the player,
    /// because an edit must show in the next frame (#41). The jobs of these chunks that run on the workers become old.
    /// </summary>
    /// <param name="positions">The world positions of the changed voxels. The caller already changed them in <see cref="Voxels"/>.</param>
    /// <param name="output">The list that receives the new meshes, one for each chunk. The method adds to the list.</param>
    /// <remarks>
    /// A chunk whose requested neighbors are not all generated does not get a mesh now. It gets its mesh when its neighbors arrive.
    /// </remarks>
    public void MeshAfterEdit(ReadOnlySpan<int3> positions, List<BuiltChunk> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        lock (_lock)
        {
            HashSet<int3> changed = _changedChunks;
            changed.Clear();
            _outputIndex.Clear();
            Span<int3> touched = stackalloc int3[4];
            foreach (int3 position in positions)
            {
                int count = Voxels.GetChunksToMesh(position, touched);
                foreach (int3 chunk in touched[..count])
                {
                    changed.Add(chunk);
                }
            }

            foreach (int3 chunk in changed)
            {
                ChunkEntry entry = _chunks[chunk];
                entry.Version++;
                if (!IsReadyForMesh(chunk))
                {
                    continue;
                }

                long start = Stopwatch.GetTimestamp();
                Voxels.TryGetChunk(chunk, out ushort[,,]? voxels);
                ChunkMeshData mesh = ChunkMeshData.Build(_mainMesher.Mesh(voxels!, Voxels, chunk), _palette);
                Interlocked.Add(ref _meshingTicks, Stopwatch.GetTimestamp() - start);
                Deliver(chunk, entry, entry.Version, mesh, output);
            }
        }
    }

    /// <summary>
    /// Receives the results of the workers. Call it one time in each frame, on the main thread.
    /// It adds the generated chunks to <see cref="Voxels"/>, starts their meshes, and gives the finished meshes.
    /// </summary>
    /// <param name="output">The list that receives the new meshes. The method adds to the list. The caller uploads them to the GPU.</param>
    /// <param name="maxResults">The maximum number of results to receive in this call. The other results wait for the next call.</param>
    /// <returns>The number of results that the method received.</returns>
    public int Update(List<BuiltChunk> output, int maxResults = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(output);
        lock (_lock)
        {
            _outputIndex.Clear();
        }

        int received = 0;
        while (received < maxResults && _results.Reader.TryRead(out Result result))
        {
            received++;
            lock (_lock)
            {
                if (result.Voxels is ushort[,,] voxels)
                {
                    OnGenerated(result.Position, voxels);
                }
                else if (result.IsGeneration)
                {
                    OnGenerationFailed(result.Position);
                }
                else if (result.Mesh is ChunkMeshData mesh && _chunks.TryGetValue(result.Position, out ChunkEntry? entry))
                {
                    Deliver(result.Position, entry, result.Version, mesh, output);
                }
            }

            // The result left the channel. Thus its job is complete.
            Interlocked.Decrement(ref _pendingJobs);
        }

        return received;
    }

    /// <summary>
    /// Gets the state of a chunk.
    /// </summary>
    /// <param name="position">The position of the chunk.</param>
    /// <returns>The state, or <see cref="ChunkState.None"/> if nobody requested the chunk.</returns>
    public ChunkState GetState(int3 position)
    {
        lock (_lock)
        {
            return _chunks.TryGetValue(position, out ChunkEntry? entry) ? entry.State : ChunkState.None;
        }
    }

    /// <summary>
    /// Stops the workers cooperatively, and waits until they stop. The jobs that wait in the queue do not run.
    /// A job that runs continues until its end. If a worker does not stop in 10 s, the error method gets a <see cref="TimeoutException"/>.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _jobs.Writer.TryComplete();
        _stop.Cancel();
        long deadline = Stopwatch.GetTimestamp() + (10 * Stopwatch.Frequency);
        bool allStopped = true;
        foreach (Thread worker in _workers)
        {
            TimeSpan left = Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), deadline);
            if (!worker.Join(left > TimeSpan.Zero ? left : TimeSpan.Zero))
            {
                allStopped = false;
                ReportError("Stop of the chunk workers", new TimeoutException($"The thread \"{worker.Name}\" did not stop in 10 s."));
            }
        }

        // A worker that did not stop can still read the token. Then the token source stays for the garbage collector.
        if (allStopped)
        {
            _stop.Dispose();
        }
    }

    // Under the lock.
    private void OnGenerated(int3 position, ushort[,,] voxels)
    {
        if (!_chunks.TryGetValue(position, out ChunkEntry? entry) || entry.State != ChunkState.Requested)
        {
            return;
        }

        Voxels.AddChunk(position, voxels);
        entry.State = ChunkState.Generated;

        // The chunk can now get its mesh, and its neighbors get a new mesh: their faces at the border with this chunk change.
        ScheduleMesh(position);
        int3 size = Voxels.ChunkSize;
        for (int direction = 0; direction < 6; direction++)
        {
            int3 neighbor = position + GreedyMesher.NeighborOffset(size, direction);
            if (_chunks.TryGetValue(neighbor, out ChunkEntry? neighborEntry) && neighborEntry.State != ChunkState.Requested)
            {
                neighborEntry.Version++;
                ScheduleMesh(neighbor);
            }
        }
    }

    // Under the lock. The worker already sent the exception to the error method. The chunk leaves the streamer, thus its neighbors
    // do not wait for it: for them, its voxels are air. A new request can try again.
    private void OnGenerationFailed(int3 position)
    {
        if (!_chunks.TryGetValue(position, out ChunkEntry? entry) || entry.State != ChunkState.Requested)
        {
            return;
        }

        _chunks.Remove(position);
        int3 size = Voxels.ChunkSize;
        for (int direction = 0; direction < 6; direction++)
        {
            ScheduleMesh(position + GreedyMesher.NeighborOffset(size, direction));
        }
    }

    // Under the lock. A new mesh job makes all the older jobs of the chunk old.
    private void ScheduleMesh(int3 position)
    {
        if (!IsReadyForMesh(position))
        {
            return;
        }

        ChunkEntry entry = _chunks[position];
        entry.Version++;
        Enqueue(new Job(position, entry.Version, ChunkSnapshot.Take(Voxels, position, entry.Version)));
    }

    // Under the lock. A chunk is ready when it is generated and all its requested neighbors are generated.
    // A neighbor that nobody requested does not stop the mesh: its voxels are air.
    private bool IsReadyForMesh(int3 position)
    {
        if (!_chunks.TryGetValue(position, out ChunkEntry? entry) || entry.State == ChunkState.Requested)
        {
            return false;
        }

        int3 size = Voxels.ChunkSize;
        for (int direction = 0; direction < 6; direction++)
        {
            if (_chunks.TryGetValue(position + GreedyMesher.NeighborOffset(size, direction), out ChunkEntry? neighbor) && neighbor.State == ChunkState.Requested)
            {
                return false;
            }
        }

        return true;
    }

    // Under the lock. Only a mesh of the current version, newer than the last delivered mesh, goes to the caller.
    private void Deliver(int3 position, ChunkEntry entry, int version, ChunkMeshData mesh, List<BuiltChunk> output)
    {
        if (version != entry.Version || version <= entry.DeliveredVersion)
        {
            return;
        }

        if (entry.DeliveredVersion < 0)
        {
            DeliveredCount++;
        }

        entry.DeliveredVersion = version;
        entry.State = ChunkState.Delivered;

        // A chunk can get two meshes in one call: a neighbor arrives after its first mesh, and a fast worker makes the next one.
        // The newer mesh replaces the older one, because the renderer accepts a chunk only one time in one batch.
        var built = new BuiltChunk(position, mesh);
        if (_outputIndex.TryGetValue(position, out int index))
        {
            output[index] = built;
        }
        else
        {
            _outputIndex.Add(position, output.Count);
            output.Add(built);
        }
    }

    private void Enqueue(Job job)
    {
        Interlocked.Increment(ref _pendingJobs);
        if (!_jobs.Writer.TryWrite(job))
        {
            // The streamer stops: the job does not run.
            Interlocked.Decrement(ref _pendingJobs);
        }
    }

    private void Work(CancellationToken stop)
    {
        // One mesher for each worker: a mesher keeps buffers, and it is not safe for many threads.
        var mesher = new GreedyMesher();
        ChannelReader<Job> reader = _jobs.Reader;
        while (!stop.IsCancellationRequested)
        {
            if (!reader.TryRead(out Job job))
            {
                try
                {
                    if (!reader.WaitToReadAsync(stop).AsTask().GetAwaiter().GetResult())
                    {
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                continue;
            }

            _results.Writer.TryWrite(Run(job, mesher));
        }
    }

    // An empty result also goes to the main thread, because Update counts the complete jobs.
    private Result Run(Job job, GreedyMesher mesher)
    {
        try
        {
            long start = Stopwatch.GetTimestamp();
            if (job.Snapshot is null)
            {
                Result generated = new(job.Position, 0, true, _generation.GenerateChunkAt(job.Position), null);
                Interlocked.Add(ref _generationTicks, Stopwatch.GetTimestamp() - start);
                return generated;
            }

            if (IsOld(job))
            {
                return new Result(job.Position, job.Version, false, null, null);
            }

            MeshJobStarting?.Invoke(job.Position, job.Version);
            Result meshed = new(job.Position, job.Version, false, null, ChunkMeshData.Build(mesher.Mesh(job.Snapshot), _palette));
            Interlocked.Add(ref _meshingTicks, Stopwatch.GetTimestamp() - start);
            return meshed;
        }
        catch (Exception exception)
        {
            ReportError(job.Snapshot is null ? $"Generation of the chunk {Format(job.Position)}" : $"Mesh of the chunk {Format(job.Position)}", exception);
            return new Result(job.Position, job.Version, job.Snapshot is null, null, null);
        }
    }

    // A newer job of the same chunk is in the queue, or a mesh after an edit replaced it. Its result cannot be delivered.
    private bool IsOld(Job job)
    {
        lock (_lock)
        {
            return !_chunks.TryGetValue(job.Position, out ChunkEntry? entry) || entry.Version != job.Version;
        }
    }

    // An exception of the error method must not stop a worker, because the worker must send the result of its job.
    private void ReportError(string job, Exception exception)
    {
        try
        {
            _onError(job, exception);
        }
        catch (Exception callbackException)
        {
            Trace.TraceError($"{job}: {exception}. The error method failed: {callbackException}");
        }
    }

    private static string Format(int3 position) => $"({position.X}, {position.Y}, {position.Z})";

    // A generation job has no snapshot. A mesh job has the snapshot of its version.
    private readonly record struct Job(int3 Position, int Version, ChunkSnapshot? Snapshot);

    // A generation result has voxels. A mesh result has mesh data. A failed job has neither.
    private readonly record struct Result(int3 Position, int Version, bool IsGeneration, ushort[,,]? Voxels, ChunkMeshData? Mesh);

    private sealed class ChunkEntry
    {
        public ChunkState State { get; set; } = ChunkState.Requested;

        public int Version { get; set; }

        public int DeliveredVersion { get; set; } = -1;
    }
}

/// <summary>
/// The state of a chunk in the <see cref="ChunkStreamer"/>.
/// </summary>
public enum ChunkState
{
    /// <summary>
    /// Nobody requested the chunk.
    /// </summary>
    None,

    /// <summary>
    /// A worker generates the chunk, or the chunk waits for a worker.
    /// </summary>
    Requested,

    /// <summary>
    /// The voxel world has the chunk. Its mesh waits for its neighbors or for a worker.
    /// </summary>
    Generated,

    /// <summary>
    /// <see cref="ChunkStreamer.Update"/> or <see cref="ChunkStreamer.MeshAfterEdit"/> gave a mesh of the chunk to the caller at least one time.
    /// The caller uploads it to the GPU. The state stays the same while a newer mesh waits for a worker.
    /// </summary>
    Delivered,
}
