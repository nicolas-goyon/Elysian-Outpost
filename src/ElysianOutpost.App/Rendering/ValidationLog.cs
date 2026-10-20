// SPDX-License-Identifier: MPL-2.0

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Brutal;
using Brutal.Logging;
using Brutal.VulkanApi;

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// Receives the messages of the Vulkan validation layers, writes them to the log and counts them.
/// </summary>
/// <remarks>
/// The validation layers can call the callback from other threads. Thus the counters use atomic operations.
/// </remarks>
internal static unsafe class ValidationLog
{
    private static readonly LogCategory Log = LogCategory.Make("Vulkan validation");

    private static int _errorCount;
    private static int _warningCount;

    /// <summary>
    /// Gets the number of error messages since the start of the app.
    /// </summary>
    public static int ErrorCount => Volatile.Read(ref _errorCount);

    /// <summary>
    /// Gets the number of warning messages since the start of the app.
    /// </summary>
    public static int WarningCount => Volatile.Read(ref _warningCount);

    /// <summary>
    /// Makes the settings of a debug messenger that sends the warnings and the errors to this class.
    /// </summary>
    /// <returns>The settings for <c>vkCreateDebugUtilsMessengerEXT</c> or for the <c>pNext</c> chain of the instance.</returns>
    public static VkDebugUtilsMessengerCreateInfoEXT CreateMessengerInfo() => new()
    {
        MessageSeverity = VkDebugUtilsMessageSeverityFlagsEXT.WarningBitEXT | VkDebugUtilsMessageSeverityFlagsEXT.ErrorBitEXT,
        MessageType = VkDebugUtilsMessageTypeFlagsEXT.GeneralBitEXT
            | VkDebugUtilsMessageTypeFlagsEXT.ValidationBitEXT
            | VkDebugUtilsMessageTypeFlagsEXT.PerformanceBitEXT,
        PfnUserCallback = new PfnVkDebugUtilsMessengerCallbackEXT(&OnMessage),
    };

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static Bool32 OnMessage(
        VkDebugUtilsMessageSeverityFlagsEXT severity,
        VkDebugUtilsMessageTypeFlagsEXT types,
        VkDebugUtilsMessengerCallbackDataEXT* data,
        void* userData)
    {
        // An exception must not leave this callback, because the caller is native code.
        try
        {
            string message = Marshal.PtrToStringUTF8((nint)data->Message) ?? string.Empty;
            if (severity.HasFlag(VkDebugUtilsMessageSeverityFlagsEXT.ErrorBitEXT))
            {
                Interlocked.Increment(ref _errorCount);
                Log.Error($"{message}");
            }
            else if (severity.HasFlag(VkDebugUtilsMessageSeverityFlagsEXT.WarningBitEXT))
            {
                Interlocked.Increment(ref _warningCount);
                Log.Warning($"{message}");
            }
        }
        catch (Exception)
        {
            // The log is not available. The message has no other destination.
        }

        // The Vulkan specification asks the callback to return false.
        return false;
    }
}
