// SPDX-License-Identifier: MPL-2.0

using System.Globalization;
using Brutal;
using Brutal.GlfwApi;
using Brutal.Logging;
using Brutal.Numerics;
using ElysianOutpost.App;
using ElysianOutpost.App.Logging;
using ElysianOutpost.App.Rendering;
using Microsoft.Extensions.Logging;

// The option "--exit-after <seconds>" closes the window after a time limit, for automatic tests.
// The limit is more than 0 seconds and not more than one day.
const double MaxTimeLimitSeconds = 86_400;
TimeSpan? timeLimit = null;
if (args is ["--exit-after", string value]
    && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
    && seconds > 0
    && seconds <= MaxTimeLimitSeconds)
{
    timeLimit = TimeSpan.FromSeconds(seconds);
}
else if (args.Length > 0)
{
    Console.Error.WriteLine($"Usage: ElysianOutpost.App [--exit-after <seconds>], with 0 < seconds <= {MaxTimeLimitSeconds}");
    return 2;
}

// The log file is in the folder of the executable. Each start of the app replaces it.
// If the app cannot write the file, for example in a read-only folder, the logs go only to the console.
string logFile = Path.Combine(AppContext.BaseDirectory, "logs", "app.log");
using var consoleSink = new TextWriterLoggerProvider(Console.Out, ownsWriter: false);
using TextWriterLoggerProvider? fileSink = OpenLogFile(logFile, out string? logFileError);
LogSystem.Initialize(builder =>
{
    builder.SetMinimumLevel(LogLevel.Information).AddProvider(consoleSink);
    if (fileSink is not null)
    {
        builder.AddProvider(fileSink);
    }
});

var log = LogCategory.Make("App");
log.Info($"Elysian Outpost starts on {Platform.CurrentFamily}.");
if (fileSink is null)
{
    log.Warning($"The app cannot write the log file {logFile}: {logFileError} The logs go only to the console.");
}
else
{
    log.Info($"Log file: {logFile}");
}

// The Vulkan bindings need this fallback on Linux before their first use. See VulkanLibrary.
VulkanLibrary.AddLinuxFallback();

Glfw.ErrorCallback += (code, description) => Glfw.Log.Error($"GLFW error {code}: {description}");
if (!Glfw.Init())
{
    log.Critical($"GLFW did not start.");
    return 1;
}

try
{
    // The renderer uses Vulkan. Thus the window has no OpenGL context.
    Glfw.WindowHint(GlfwWindowHint.ClientApi, Glfw.Constants.GLFW_NO_API);
    GlfwWindow window = Glfw.CreateWindow(new GlfwWindow.CreateInfo
    {
        Title = "Elysian Outpost",
        Size = new int2(1280, 720),
    });

    try
    {
        log.Info($"The window is open: {window.Width}x{window.Height} pixels, platform {Glfw.GetPlatform()}.");

        // The renderer uses the surface of the window. Thus the app releases the renderer before the window.
        using var renderer = new VulkanRenderer(window, LogCategory.Make("Renderer"));
        new MainLoop(window, renderer, log, timeLimit).Run();
    }
    finally
    {
        window.Dispose();
    }
}
catch (Exception exception)
{
    log.Critical(exception);
    return 1;
}
finally
{
    Glfw.Terminate();
}

log.Info($"Elysian Outpost stops.");
return 0;

static TextWriterLoggerProvider? OpenLogFile(string path, out string? error)
{
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        error = null;
        return new TextWriterLoggerProvider(new StreamWriter(path, append: false), ownsWriter: true);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        error = exception.Message;
        return null;
    }
}
