// SPDX-License-Identifier: MPL-2.0

using System.Globalization;
using System.Runtime.InteropServices;

namespace ElysianOutpost.Core.Diagnostics;

/// <summary>
/// The operating system, the CPU and the RAM of the computer (#81).
/// </summary>
/// <param name="OperatingSystem">The name and the version of the operating system (<see cref="RuntimeInformation.OSDescription"/>).</param>
/// <param name="Architecture">The architecture of the operating system, for example <c>X64</c>.</param>
/// <param name="CpuModel">The model of the CPU, or <c>unknown</c>.</param>
/// <param name="CpuCores">The number of physical CPU cores, or 0 if it is not known.</param>
/// <param name="CpuThreads">The number of logical processors (hardware threads) that the process can use.</param>
/// <param name="TotalRamBytes">The total physical RAM, in bytes, or 0 if it is not known.</param>
/// <param name="AvailableRamBytes">The available physical RAM, in bytes, or 0 if it is not known.</param>
/// <param name="DotNetVersion">The version of the .NET runtime.</param>
public sealed record SystemInfo(
    string OperatingSystem,
    string Architecture,
    string CpuModel,
    int CpuCores,
    int CpuThreads,
    long TotalRamBytes,
    long AvailableRamBytes,
    string DotNetVersion)
{
    /// <summary>
    /// Collects the values of this computer. On Linux, the values come from <c>/proc/cpuinfo</c> and <c>/proc/meminfo</c>.
    /// On Windows, they come from the registry and from Windows functions. A value that is not available is <c>unknown</c> or 0.
    /// </summary>
    /// <returns>The values.</returns>
    public static SystemInfo Collect()
    {
        string cpuModel = "unknown";
        int cores = 0;
        long totalRam = 0;
        long availableRam = 0;
        try
        {
            if (System.OperatingSystem.IsLinux())
            {
                (cpuModel, cores) = ParseCpuInfo(File.ReadAllText("/proc/cpuinfo"));
                (totalRam, availableRam) = ParseMemInfo(File.ReadAllText("/proc/meminfo"));
            }
            else if (System.OperatingSystem.IsWindows())
            {
                cpuModel = WindowsSystem.GetCpuModel() ?? cpuModel;
                cores = WindowsSystem.GetCoreCount();
                (totalRam, availableRam) = WindowsSystem.GetMemory();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            // The report keeps the values that it has.
        }

        if (totalRam == 0)
        {
            totalRam = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        }

        return new SystemInfo(
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            cpuModel,
            cores,
            Environment.ProcessorCount,
            totalRam,
            availableRam,
            RuntimeInformation.FrameworkDescription);
    }

    /// <summary>
    /// Reads the CPU model and the number of physical cores from the text of <c>/proc/cpuinfo</c>.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    /// <returns>The model, or <c>unknown</c>, and the number of distinct pairs of <c>physical id</c> and <c>core id</c>, or 0.</returns>
    internal static (string Model, int Cores) ParseCpuInfo(string text)
    {
        string model = "unknown";
        var cores = new HashSet<(string, string)>();
        string physical = "0";
        string? core = null;
        foreach (string line in text.Split('\n'))
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0)
            {
                // An empty line ends the block of one logical processor.
                if (core is not null)
                {
                    cores.Add((physical, core));
                }

                physical = "0";
                core = null;
                continue;
            }

            string key = line[..colon].Trim();
            string value = line[(colon + 1)..].Trim();
            switch (key)
            {
                case "model name" when model == "unknown":
                    model = value;
                    break;
                case "physical id":
                    physical = value;
                    break;
                case "core id":
                    core = value;
                    break;
            }
        }

        if (core is not null)
        {
            cores.Add((physical, core));
        }

        return (model, cores.Count);
    }

    /// <summary>
    /// Reads the total RAM and the available RAM from the text of <c>/proc/meminfo</c>.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    /// <returns>The values of <c>MemTotal</c> and <c>MemAvailable</c>, in bytes, or 0 for a value that the text does not have.</returns>
    internal static (long Total, long Available) ParseMemInfo(string text)
    {
        long total = 0;
        long available = 0;
        foreach (string line in text.Split('\n'))
        {
            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long kibibytes))
            {
                continue;
            }

            // The file gives the values in kibibytes, with the unit "kB".
            switch (parts[0])
            {
                case "MemTotal:":
                    total = kibibytes * 1024;
                    break;
                case "MemAvailable:":
                    available = kibibytes * 1024;
                    break;
            }
        }

        return (total, available);
    }
}
