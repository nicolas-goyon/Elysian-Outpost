// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;
using ElysianOutpost.Core.Diagnostics;

namespace ElysianOutpost.Core.Tests.Diagnostics;

public sealed class HardwareReportTests
{
    // Two logical processors on each core (hyper-threading): 4 logical processors, 2 cores.
    private const string CpuInfo = """
        processor	: 0
        model name	: Example CPU 3000 @ 3.00GHz
        physical id	: 0
        core id		: 0

        processor	: 1
        model name	: Example CPU 3000 @ 3.00GHz
        physical id	: 0
        core id		: 1

        processor	: 2
        model name	: Example CPU 3000 @ 3.00GHz
        physical id	: 0
        core id		: 0

        processor	: 3
        model name	: Example CPU 3000 @ 3.00GHz
        physical id	: 0
        core id		: 1
        """;

    [Fact]
    public void ParseCpuInfo_GivesTheModelAndTheNumberOfPhysicalCores()
    {
        (string model, int cores) = SystemInfo.ParseCpuInfo(CpuInfo.ReplaceLineEndings("\n"));

        Assert.Equal("Example CPU 3000 @ 3.00GHz", model);
        Assert.Equal(2, cores);
    }

    // Two sockets with the same core IDs have different physical IDs. A virtual machine can give no core ID: then the number is 0.
    [Fact]
    public void ParseCpuInfo_TwoSocketsOrNoCoreId()
    {
        string twoSockets = "physical id : 0\ncore id : 0\n\nphysical id : 1\ncore id : 0\n";
        Assert.Equal(2, SystemInfo.ParseCpuInfo(twoSockets).Cores);
        Assert.Equal(("unknown", 0), SystemInfo.ParseCpuInfo("processor : 0\nvendor_id : Example\n"));
    }

    [Fact]
    public void ParseMemInfo_GivesTheTotalAndTheAvailableRamInBytes()
    {
        const string memInfo = "MemTotal:        8030244 kB\nMemFree:          512000 kB\nMemAvailable:    4000000 kB\n";

        Assert.Equal((8_030_244L * 1024, 4_000_000L * 1024), SystemInfo.ParseMemInfo(memInfo));
        Assert.Equal((0L, 0L), SystemInfo.ParseMemInfo("nothing"));
    }

    [Fact]
    public void Collect_GivesTheValuesOfThisComputer()
    {
        SystemInfo system = SystemInfo.Collect();

        Assert.False(string.IsNullOrWhiteSpace(system.OperatingSystem));
        Assert.False(string.IsNullOrWhiteSpace(system.CpuModel));
        Assert.Equal(Environment.ProcessorCount, system.CpuThreads);
        Assert.InRange(system.CpuCores, 0, system.CpuThreads);
        Assert.True(system.TotalRamBytes > 0);
        Assert.InRange(system.AvailableRamBytes, 0, system.TotalRamBytes);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
        {
            Assert.NotEqual("unknown", system.CpuModel);
            Assert.True(system.CpuCores > 0);
            Assert.True(system.AvailableRamBytes > 0);
        }
    }

    [Theory]
    [InlineData(0x10DE, "NVIDIA")]
    [InlineData(0x1002, "AMD")]
    [InlineData(0x8086, "Intel")]
    [InlineData(0x10005, "Mesa")]
    [InlineData(0x1234, "0x1234")]
    public void VendorName_GivesTheNameOfAKnownVendor(int vendorId, string expected)
    {
        Assert.Equal(expected, GpuInfo.VendorName(vendorId));
    }

    // Task 4 of #81: the report has no personal data. The JSON text has the values of task 1, with camel-case names.
    [Fact]
    public void ToJson_GivesAllTheValues_AndNoPersonalData()
    {
        var report = new HardwareReport
        {
            AppVersion = "1.0.0",
            System = SystemInfo.Collect(),
            Gpu = new GpuInfo("Example GPU", "Intel", "IntegratedGpu", "25.2.8", "1.4.318", 2_147_483_648),
            Monitors = [new MonitorInfo(true, 1920, 1080, 60, 1.25f), new MonitorInfo(false, 2560, 1440, 144, 1f)],
            Window = new WindowInfo(1280, 720, 1600, 900, "FIFO", true, "Wayland"),
        };

        string json = report.ToJson();
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Equal(1, root.GetProperty("formatVersion").GetInt32());
        foreach (string name in new[] { "operatingSystem", "architecture", "cpuModel", "cpuCores", "cpuThreads", "totalRamBytes", "availableRamBytes" })
        {
            Assert.True(root.GetProperty("system").TryGetProperty(name, out _), name);
        }

        Assert.Equal("Example GPU", root.GetProperty("gpu").GetProperty("name").GetString());
        Assert.Equal(2_147_483_648, root.GetProperty("gpu").GetProperty("deviceLocalMemoryBytes").GetInt64());
        Assert.Equal(144, root.GetProperty("monitors")[1].GetProperty("refreshRate").GetInt32());
        Assert.True(root.GetProperty("window").GetProperty("verticalSync").GetBoolean());
        Assert.Equal("1.0.0", root.GetProperty("appVersion").GetString());

        // The user name and the computer name of the test computer are not in the text. Very short names can be in other words.
        foreach (string personal in new[] { Environment.UserName, Environment.MachineName })
        {
            if (personal.Length >= 4)
            {
                Assert.DoesNotContain(personal, json, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
