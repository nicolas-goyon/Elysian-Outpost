// SPDX-License-Identifier: MPL-2.0

using System.Reflection;
using System.Text;
using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.ImGuiApi.Abstractions;
using Brutal.Logging;
using ElysianOutpost.App.Rendering;
using ElysianOutpost.Core.Diagnostics;

namespace ElysianOutpost.App.Ui;

/// <summary>
/// The in-game console (#37): the console window of BRUTAL, with the messages of the console log.
/// The console key opens and closes it (see <see cref="Input.InputSettings.ConsoleKey"/>).
/// </summary>
/// <remarks>
/// <para>
/// All threads add their messages to the <see cref="ConsoleLog"/>. Only the main thread calls the methods of this class.
/// In each frame, <see cref="Update"/> gives the new messages of the log to the console window.
/// Thus the console window shows the same messages as the log, and never more than <see cref="ConsoleLog.Capacity"/> messages.
/// </para>
/// <para>
/// The console window is a bar at the bottom of the screen. When it opens, it takes the keyboard focus.
/// When another ImGui window takes the focus, the console window closes.
/// Enter executes the command line. Enter on an empty command line closes the console window.
/// The up arrow and the down arrow show the previous command lines.
/// </para>
/// <para>
/// The commands are the commands of the legacy console, with the same names and descriptions:
/// <c>log</c>, <c>clear</c>, <c>close</c>, <c>copy</c> and <c>help</c>. The app has no NavMesh. Thus it has no <c>navmesh_bake</c> command.
/// The terminal of BRUTAL reads the command line. A semicolon separates two commands on one line.
/// </para>
/// <para>
/// The console window cannot remove only the oldest lines. When the log removes a message that the window shows,
/// the window removes all its lines and receives all the messages of the log again.
/// Then the time at the start of each line in the window is the time of this copy.
/// The text of a log message has the correct time.
/// </para>
/// </remarks>
internal sealed class InGameConsole
{
    /// <summary>
    /// The maximum number of characters in a message of the console log.
    /// </summary>
    /// <remarks>
    /// The console window keeps the text that it receives in one frame in a buffer of 1 MiB, and it ignores the text after this limit.
    /// A character has a maximum of 3 bytes in UTF-8. Thus 1,000 messages with 340 characters have less than 1 MiB.
    /// </remarks>
    public const int MaxMessageLength = 340;

    private readonly ConsoleWindow _window = new();
    private readonly ConsoleLog _log;
    private readonly ConsoleLogReader _reader;
    private readonly LogCategory _commandLog;
    private readonly string _helpText;

    // The color of the text for each value of ConsoleMessageLevel.
    private readonly ImColor8[] _levelColors;

    /// <summary>
    /// Initializes a new instance of the <see cref="InGameConsole"/> class. The console window is closed.
    /// </summary>
    /// <param name="log">The console log. Use <see cref="CreateLog"/> to make it.</param>
    /// <param name="linearColors"><c>true</c> if the ImGui colors must be linear values (see <see cref="ImGuiHost.LinearColors"/>).</param>
    /// <param name="commandLog">The log category of the <c>log</c> command.</param>
    public InGameConsole(ConsoleLog log, bool linearColors, LogCategory commandLog)
    {
        _log = log;
        _reader = new ConsoleLogReader(log);
        _commandLog = commandLog;
        _levelColors =
        [
            Color(ConsoleWindow.TraceColor, linearColors),
            Color(ConsoleWindow.DebugColor, linearColors),
            Color(ConsoleWindow.InfoColor, linearColors),
            Color(ConsoleWindow.WarningColor, linearColors),
            Color(ConsoleWindow.ErrorColor, linearColors),
            Color(ConsoleWindow.CriticalColor, linearColors),
        ];

        // The order of the legacy console. The terminal of BRUTAL also has the commands "help" and "clear".
        // These commands write directly to the console window, not to the console log. Commands with the same names replace them.
        Delegate[] commands = [LogCommand, ClearCommand, CloseCommand, CopyCommand, HelpCommand];
        var help = new StringBuilder("Available Commands:");
        foreach (Delegate command in commands)
        {
            _window.Terminal.RegisterCommand(command);
            TerminalActionAttribute action = command.Method.GetCustomAttribute<TerminalActionAttribute>()!;
            help.Append('\n').Append(action.CommandName).Append(": ").Append(action.Description);
        }

        _helpText = help.ToString();

        // The terminal gives the errors of the command line with this event, for example a wrong number of arguments.
        _window.Terminal.OnOutput += OnTerminalOutput;

        // By default, Enter ends the edit of a text field. Then the console window must activate the command line again,
        // and ImGui ignores the characters of the next frames. With this value, the command line stays active after Enter.
        // The app has no other ImGui text field.
        ImGui.GetIO().ConfigInputTextEnterKeepActive = true;
    }

    /// <summary>
    /// Gets a value that tells if the console window is open.
    /// </summary>
    public bool IsOpen => _window.IsOpen;

    /// <summary>
    /// Makes the log for the console: it keeps the last 1,000 messages, with a maximum of <see cref="MaxMessageLength"/> characters each.
    /// </summary>
    /// <returns>The console log.</returns>
    public static ConsoleLog CreateLog() => new(ConsoleLog.DefaultCapacity, MaxMessageLength);

    /// <summary>
    /// Opens the console window if it is closed, or closes it if it is open.
    /// </summary>
    public void Toggle() => _window.HandleInputAction(ConsoleWindow.Actions.Toggle);

    /// <summary>
    /// Gives the new messages of the console log to the console window. Call it one time in each frame, also when the window is closed.
    /// </summary>
    public void Update()
    {
        ConsoleLogChanges changes = _reader.ReadChanges();
        if (changes.Restart)
        {
            _window.ClearConsole();
        }

        foreach (ref readonly ConsoleMessage message in changes.Messages)
        {
            _window.Print(message.Text.AsSpan(), _levelColors[(int)message.Level]);
        }

        // The console window adds the new lines to its list when it swaps its buffers. Draw also swaps the buffers.
        // When the window is closed, this call keeps the buffer of the new lines small.
        _window.SwapBuffers();
    }

    /// <summary>
    /// Draws the console window in the current ImGui frame and reads the keys of the command line. Call it only when the window is open.
    /// </summary>
    /// <param name="frameSeconds">The time since the previous frame, in seconds.</param>
    public void Draw(float frameSeconds)
    {
        ImGuiViewportPtr viewport = ImGui.GetMainViewport();
        _window.Draw(viewport.Pos, viewport.Size, frameSeconds);
        if (!_window.IsOpen)
        {
            return;
        }

        // The keys go through ImGui, as the characters of the command line. Thus the command line has all the characters before Enter.
        if (ImGui.IsKeyPressed(ImGuiKey.Enter, false) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter, false))
        {
            _window.HandleInputAction(ConsoleWindow.Actions.Submit);
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
        {
            _window.HandleInputAction(ConsoleWindow.Actions.Up);
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
        {
            _window.HandleInputAction(ConsoleWindow.Actions.Down);
        }
    }

    [TerminalAction("log", "Logs a message to the console.", ArgParseMode.Unparsed)]
    private void LogCommand(string message)
    {
        // The command sends the message to the log system. Thus the log file also receives it.
        if (message.Length == 0)
        {
            _log.Add("Usage: log <message>", ConsoleMessageLevel.Information);
        }
        else
        {
            _commandLog.Info($"{message}");
        }
    }

    [TerminalAction("clear", "Clears the console output.")]
    private void ClearCommand() => _log.Clear();

    [TerminalAction("close", "Closes the console UI.")]
    private void CloseCommand() => _window.Hide();

    // The legacy console copies the text of all its lines. The console log has the same text.
    [TerminalAction("copy", "Copies the console output to clipboard.")]
    private void CopyCommand() => Glfw.SetClipboard(_log.GetText());

    [TerminalAction("help", "Displays a list of available commands.")]
    private void HelpCommand() => _log.Add(_helpText, ConsoleMessageLevel.Information);

    private void OnTerminalOutput(string text, TerminalInterfaceOutputType type)
    {
        if (text.Length > 0)
        {
            _log.Add(text, type == TerminalInterfaceOutputType.Error ? ConsoleMessageLevel.Error : ConsoleMessageLevel.Information);
        }
    }

    // The colors of the console window are sRGB values. With an sRGB swapchain, ImGui needs linear values (see ImGuiPass).
    private static ImColor8 Color(ImColor8 color, bool linear) =>
        linear ? (ImColor8)ColorSpace.SrgbToLinear(color.AsFloat4()) : color;
}
