// SPDX-License-Identifier: MPL-2.0

using System.Globalization;
using Brutal;
using Brutal.GlfwApi;
using Brutal.Logging;
using Brutal.Numerics;
using ElysianOutpost.App;
using ElysianOutpost.App.Logging;
using Microsoft.Extensions.Logging;

// The option "--exit-after <seconds>" closes the window after a time limit, for automatic tests.
TimeSpan? timeLimit = null;
if (args is ["--exit-after", string value]
    && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
    && seconds > 0)
{
    timeLimit = TimeSpan.FromSeconds(seconds);
}
else if (args.Length > 0)
{
    Console.Error.WriteLine("Usage: ElysianOutpost.App [--exit-after <seconds>]");
    return 2;
}

// The log file is in the folder of the executable. Each start of the app replaces it.
string logFile = Path.Combine(AppContext.BaseDirectory, "logs", "app.log");
Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);
using var consoleSink = new TextWriterLoggerProvider(Console.Out, ownsWriter: false);
using var fileSink = new TextWriterLoggerProvider(new StreamWriter(logFile, append: false), ownsWriter: true);
LogSystem.Initialize(builder => builder
    .SetMinimumLevel(LogLevel.Information)
    .AddProvider(consoleSink)
    .AddProvider(fileSink));

var log = LogCategory.Make("App");
log.Info($"Elysian Outpost starts on {Platform.CurrentFamily}. Log file: {logFile}");

Glfw.ErrorCallback += (code, description) => Glfw.Log.Error($"GLFW error {code}: {description}");
if (!Glfw.Init())
{
    log.Critical($"GLFW did not start.");
    return 1;
}

try
{
    // The renderer will use Vulkan (#35). Thus the window has no OpenGL context.
    Glfw.WindowHint(GlfwWindowHint.ClientApi, Glfw.Constants.GLFW_NO_API);
    GlfwWindow window = Glfw.CreateWindow(new GlfwWindow.CreateInfo
    {
        Title = "Elysian Outpost",
        Size = new int2(1280, 720),
    });

    try
    {
        log.Info($"The window is open: {window.Width}x{window.Height} pixels, platform {Glfw.GetPlatform()}.");
        new MainLoop(window, log, timeLimit).Run();
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
