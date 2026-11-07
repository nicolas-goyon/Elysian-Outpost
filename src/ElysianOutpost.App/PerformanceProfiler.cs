// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Globalization;
using Brutal.ImGuiApi.Abstractions;
using Brutal.Logging;
using ElysianOutpost.Core.Diagnostics;

namespace ElysianOutpost.App;

/// <summary>
/// Records the performance of each frame during a session, and writes it to a CSV file in the folder <c>profiles/</c> (#80).
/// </summary>
/// <remarks>
/// <para>
/// The console commands <c>profile start</c> and <c>profile stop</c> start and stop the profile. The option <c>--profile</c> of the app
/// starts a profile at the start of the main loop. The main loop stops a profile that runs when the window closes.
/// </para>
/// <para>
/// The main loop gives the values of each frame to <see cref="Record"/>. The frame time of a frame is known only at the start of the next frame.
/// Thus the profiler keeps the values of the last frame until the next call.
/// </para>
/// <para>
/// At <see cref="Stop"/>, the profiler writes the file, and writes a summary and its own cost to the log.
/// The file name contains the local date and time of the start, for example <c>profile-20261107-231500.csv</c>.
/// </para>
/// </remarks>
internal sealed class PerformanceProfiler
{
    private readonly LogCategory _log;
    private readonly string _folder;
    private FrameProfile? _profile;
    private DateTime _startTime;
    private long _startTimestamp;
    private bool _hasPending;
    private long _pendingTimestamp;
    private FrameSample _pending;
    private long _costTicks;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceProfiler"/> class.
    /// </summary>
    /// <param name="folder">The folder of the CSV files. The profiler makes it if it does not exist.</param>
    /// <param name="log">The log category of the profiler.</param>
    public PerformanceProfiler(string folder, LogCategory log)
    {
        _folder = folder;
        _log = log;
    }

    /// <summary>
    /// Gets a value that tells if a profile runs.
    /// </summary>
    public bool IsRecording => _profile is not null;

    /// <summary>
    /// Gets the console command <c>profile</c>.
    /// </summary>
    public Delegate ConsoleCommand => Command;

    /// <summary>
    /// Starts a profile. If a profile runs, nothing changes.
    /// </summary>
    public void Start()
    {
        if (_profile is not null)
        {
            _log.Info($"A performance profile already runs.");
            return;
        }

        _profile = new FrameProfile();
        _startTime = DateTime.Now;
        _startTimestamp = Stopwatch.GetTimestamp();
        _hasPending = false;
        _costTicks = 0;
        _log.Info($"The performance profile starts.");
    }

    /// <summary>
    /// Gives the values of a frame. Call it one time in each frame, at the end of the frame work, only when <see cref="IsRecording"/> is <c>true</c>.
    /// </summary>
    /// <param name="frameStartTimestamp">The <see cref="Stopwatch"/> timestamp of the start of the frame.</param>
    /// <param name="sample">The values of the frame, without <see cref="FrameSample.TimeMilliseconds"/> and <see cref="FrameSample.FrameMilliseconds"/>.</param>
    public void Record(long frameStartTimestamp, in FrameSample sample)
    {
        if (_profile is null)
        {
            return;
        }

        if (_hasPending)
        {
            _profile.Add(_pending with { FrameMilliseconds = Stopwatch.GetElapsedTime(_pendingTimestamp, frameStartTimestamp).TotalMilliseconds });
        }

        _pending = sample with { TimeMilliseconds = Stopwatch.GetElapsedTime(_startTimestamp, frameStartTimestamp).TotalMilliseconds };
        _pendingTimestamp = frameStartTimestamp;
        _hasPending = true;
    }

    /// <summary>
    /// Adds time to the cost of the profiler. The main loop measures the time to collect the values of each frame.
    /// </summary>
    /// <param name="ticks">The time, in <see cref="Stopwatch"/> ticks.</param>
    public void AddCost(long ticks) => _costTicks += ticks;

    /// <summary>
    /// Stops the profile, writes the CSV file, and writes the summary to the log. If no profile runs, nothing changes.
    /// </summary>
    public void Stop()
    {
        if (_profile is not FrameProfile profile)
        {
            _log.Info($"No performance profile runs.");
            return;
        }

        // The last frame has no next frame, thus its frame time is not known. The profile drops it.
        _profile = null;
        ProfileSummary? summary = profile.Summarize();
        if (summary is null)
        {
            _log.Info($"The performance profile stops. It has no complete frame, thus there is no file.");
            return;
        }

        long writeStart = Stopwatch.GetTimestamp();
        string path = Path.Combine(_folder, $"profile-{_startTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.csv");
        try
        {
            Directory.CreateDirectory(_folder);
            using (var writer = new StreamWriter(path, append: false))
            {
                profile.WriteCsv(writer);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _log.Error($"The performance profile cannot write the file {path}: {exception.Message}");
            path = "(no file)";
        }

        double costPerFrame = Stopwatch.GetElapsedTime(0, _costTicks).TotalMilliseconds / summary.FrameCount;
        _log.Info($"The performance profile stops. {summary} Profiler cost: {costPerFrame:F4} ms for each frame, file write {Stopwatch.GetElapsedTime(writeStart).TotalMilliseconds:F0} ms. File: {path}");
    }

    [TerminalAction("profile", "Starts or stops the performance profile: profile start, profile stop.")]
    private void Command(string action)
    {
        switch (action)
        {
            case "start":
                Start();
                break;
            case "stop":
                Stop();
                break;
            default:
                _log.Info($"Usage: profile start, or profile stop.");
                break;
        }
    }
}
