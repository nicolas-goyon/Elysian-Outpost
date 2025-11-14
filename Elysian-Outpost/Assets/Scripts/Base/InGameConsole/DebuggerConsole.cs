using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Base.InGameConsole
{
    public static class DebuggerConsole
    {
        private static Dictionary<string, ConsoleCommand> _commands = new Dictionary<string, ConsoleCommand>();

        public static event System.Action<string> OnLogEmitted;

        // private const bool _enabled = false;

        private static bool _usingUnityLog = true;

        public static void AddCommand(ConsoleCommand command)
        {
            if (!_commands.TryAdd(command.CommandName, command))
            {
                throw new System.Exception($"Command {command.CommandName} already exists");
            }
        }

        // public static void Enable()
        // {
        //     if (_enabled) return;
        //     _enabled = true;
        //     Application.logMessageReceived += HandleLog;
        // }
        //
        // public static void Disable()
        // {
        //     if (!_enabled) return;
        //     _enabled = false;
        //     Application.logMessageReceived -= HandleLog;
        // }

        private static void HandleLog(string logString, string stackTrace, LogType type)
        {
            OnLogEmitted?.Invoke(logString);
            if (!_usingUnityLog) return;
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
            System.Array.Copy(parts, 1, args, 0, args.Length);

            if (_commands.TryGetValue(commandName, out ConsoleCommand command))
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
                System.Diagnostics.StackTrace stackTraceRaw = new System.Diagnostics.StackTrace(true);
                stackTrace = stackTraceRaw.ToString();
            }

            HandleLog(message, stackTrace, LogType.Log);
        }

        public static void LogWarning(string message, bool withStackTrace = false)
        {
            string stackTrace = string.Empty;
            if (withStackTrace)
            {
                System.Diagnostics.StackTrace stackTraceRaw = new System.Diagnostics.StackTrace(true);
                stackTrace = stackTraceRaw.ToString();
            }

            HandleLog(message, stackTrace, LogType.Warning);
        }

        public static void LogError(string message, bool withStackTrace = false)
        {
            string stackTrace = string.Empty;
            if (withStackTrace)
            {
                System.Diagnostics.StackTrace stackTraceRaw = new System.Diagnostics.StackTrace(true);
                stackTrace = stackTraceRaw.ToString();
            }

            HandleLog(message, stackTrace, LogType.Error);
        }

        public static void LogException(System.Exception exception)
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


        public class ConsoleCommand
        {
            public string CommandName;
            public string Description;
            public System.Action<string[]> Execute;

            public ConsoleCommand(string commandName, string description, System.Action<string[]> execute)
            {
                CommandName = commandName;
                Description = description;
                Execute = execute;
            }
        }
    }
}