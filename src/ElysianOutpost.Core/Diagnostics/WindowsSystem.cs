// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace ElysianOutpost.Core.Diagnostics;

// The values of SystemInfo on Windows (#81): the registry and two functions of kernel32.
[SupportedOSPlatform("windows")]
internal static class WindowsSystem
{
    // RelationProcessorCore in LOGICAL_PROCESSOR_RELATIONSHIP.
    private const int RelationProcessorCore = 0;

    public static string? GetCpuModel()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        return (key?.GetValue("ProcessorNameString") as string)?.Trim();
    }

    // Counts the processor cores with GetLogicalProcessorInformationEx. Each entry of the relation RelationProcessorCore is one core.
    public static int GetCoreCount()
    {
        uint length = 0;
        GetLogicalProcessorInformationEx(RelationProcessorCore, null, ref length);
        if (length == 0)
        {
            return 0;
        }

        byte[] buffer = new byte[length];
        if (!GetLogicalProcessorInformationEx(RelationProcessorCore, buffer, ref length))
        {
            return 0;
        }

        // Each entry starts with its relation (4 bytes) and its size (4 bytes).
        int count = 0;
        for (int offset = 0; offset + 8 <= (int)length;)
        {
            int size = BitConverter.ToInt32(buffer, offset + 4);
            if (size <= 0)
            {
                break;
            }

            count += BitConverter.ToInt32(buffer, offset) == RelationProcessorCore ? 1 : 0;
            offset += size;
        }

        return count;
    }

    public static (long Total, long Available) GetMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status) ? ((long)status.TotalPhys, (long)status.AvailPhys) : (0, 0);
    }

    // DllImport, not LibraryImport: LibraryImport needs unsafe code, and the core library does not allow it.
#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLogicalProcessorInformationEx(int relationship, [Out] byte[]? buffer, ref uint returnedLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
#pragma warning restore SYSLIB1054

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}
