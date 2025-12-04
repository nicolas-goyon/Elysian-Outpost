using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Base.InGameConsole
{
    public static class DebuggerConsole
    {
        private static readonly Dictionary<string, ConsoleCommand> Commands = new();

        public static event Action<string> OnLogEmitted;

        private static bool _interceptExceptionsEnabled = false;

        private const bool UsingUnityLog = false;

        public static void AddCommand(ConsoleCommand command)
        {
            if (!Commands.TryAdd(command.CommandName, command))
            {
                throw new Exception($"Command {command.CommandName} already exists");
            }
        }

        public static void Enable()
        {
            if (_interceptExceptionsEnabled) return;
            _interceptExceptionsEnabled = true;
            Application.logMessageReceived += HandleLog;
        }

        public static void Disable()
        {
            if (!_interceptExceptionsEnabled) return;
            _interceptExceptionsEnabled = false;
            Application.logMessageReceived -= HandleLog;
        }

        private static void HandleLog(string logString, string stackTrace, LogType type)
        {
            OnLogEmitted?.Invoke(logString + (string.IsNullOrEmpty(stackTrace) ? "" : $"\n{stackTrace}"));
            if (!UsingUnityLog) return;
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                    Debug.LogError(logString);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(logString);
                    break;
                case LogType.Assert:
                    Debug.LogAssertion(logString);
                    break;
                case LogType.Log:
                    Debug.Log(logString);
                    break;
                default:
                    Debug.LogError("Unknown log type");
                    Debug.Log(logString);
                    break;
            }
        }

        public static void ExecuteCommand(string input)
        {
            string[] parts = input.Split(' ');
            string commandName = parts[0];
            string[] args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);

            if (Commands.TryGetValue(commandName, out ConsoleCommand command))
            {
                command.Execute(args);
            }
            else
            {
                OnLogEmitted?.Invoke($"Command '{commandName}' not found.");
            }
        }

        #region LoggingCommands

        public static void Log(string message, bool withStackTrace = false)
        {
            string stackTrace = string.Empty;
            if (withStackTrace)
            {
                StackTrace stackTraceRaw = new StackTrace(true);
                stackTrace = stackTraceRaw.ToString();
            }

            HandleLog(message, stackTrace, LogType.Log);
        }

        public static void LogWarning(string message, bool withStackTrace = false)
        {
            string stackTrace = string.Empty;
            if (withStackTrace)
            {
                StackTrace stackTraceRaw = new StackTrace(true);
                stackTrace = stackTraceRaw.ToString();
            }

            HandleLog(message, stackTrace, LogType.Warning);
        }

        public static void LogError(string message, bool withStackTrace = false)
        {
            string stackTrace = string.Empty;
            if (withStackTrace)
            {
                StackTrace stackTraceRaw = new StackTrace(true);
                stackTrace = stackTraceRaw.ToString();
            }

            HandleLog(message, stackTrace, LogType.Error);
        }

        public static void LogException(Exception exception)
        {
            string stackTrace = exception.StackTrace;
            HandleLog(exception.Message, stackTrace, LogType.Exception);
        }

        /**
         * Deconstructs an object and logs its fields and properties recursively. (alt function)
         */
        public static void LogDeconstruct(object obj, int maxDepth = 5)
        {
            string result = DeconstructObject(obj, maxDepth);
            HandleLog(result, string.Empty, LogType.Log);
        }

        private static string DeconstructObject(object obj, int maxDepth, int indentLevel = 0)
        {
            if (obj == null)
            {
                return "null";
            }

            if (maxDepth <= 0)
            {
                return "...";
            }

            maxDepth -= 1;


            Type type = obj.GetType();
            StringBuilder sb = new();

            string indent = new string(' ', indentLevel * 2);
            sb.AppendLine($"{indent}{type.Name} {{");

            // Log fields
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                object fieldValue = field.GetValue(obj);
                sb.AppendLine($"{indent}  {field.Name}: {DeconstructObject(fieldValue, maxDepth, indentLevel + 1)}");
            }

            // Log properties
            PropertyInfo[] properties =
                type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (PropertyInfo property in properties)
            {
                try
                {
                    if (property.GetIndexParameters().Length != 0) continue; // Skip indexers
                    object propertyValue = property.GetValue(obj);
                    sb.AppendLine(
                        $"{indent}  {property.Name}: {DeconstructObject(propertyValue, maxDepth, indentLevel + 1)}");
                }
                catch
                {
                    sb.AppendLine($"{indent}  {property.Name}: <unreadable>");
                }
            }

            sb.AppendLine($"{indent}}}");
            return sb.ToString();
        }

        #endregion

        #region UtilityCommands

        public static void HelpCommand()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Available Commands:");
            foreach (ConsoleCommand command in Commands.Values)
            {
                sb.AppendLine($"{command.CommandName}: {command.Description}");
            }

            HandleLog(sb.ToString(), string.Empty, LogType.Log);
        }

        #endregion

        public class ConsoleCommand
        {
            public readonly string CommandName;
            public readonly string Description;
            public readonly Action<string[]> Execute;

            public ConsoleCommand(string commandName, string description, Action<string[]> execute)
            {
                CommandName = commandName;
                Description = description;
                Execute = execute;
            }
        }
    }
}