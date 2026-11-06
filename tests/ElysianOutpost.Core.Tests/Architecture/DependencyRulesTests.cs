// SPDX-License-Identifier: MPL-2.0

using System.Text.Json.Nodes;

namespace ElysianOutpost.Core.Tests.Architecture;

public sealed class DependencyRulesTests
{
    private const string CoreProject = "ElysianOutpost.Core";

    private const string AppProjectFile = "../ElysianOutpost.App/ElysianOutpost.App.csproj";

    [Fact]
    public void CoreLibrary_FollowsTheDependencyRules()
    {
        var assets = ProjectAssets.Load(RepositoryPaths.CoreAssetsFile);
        Assert.Equal(CoreProject, assets.ProjectName);

        IReadOnlyList<string> violations = DependencyRules.Check(assets);

        Assert.True(violations.Count == 0, FailureMessage(violations));
    }

    // This test makes sure that the parser reads a real assets file, with direct and transitive dependencies.
    [Fact]
    public void Parse_AssetsFileOfThisProject_GivesItsDependencies()
    {
        var assets = ProjectAssets.Load(RepositoryPaths.TestsAssetsFile);

        // Microsoft.NET.Test.Sdk brings Microsoft.TestPlatform.TestHost.
        Assert.Equal("ElysianOutpost.Core.Tests", assets.ProjectName);
        Assert.Contains("Microsoft.NET.Test.Sdk", assets.DirectPackages);
        Assert.DoesNotContain("Microsoft.TestPlatform.TestHost", assets.DirectPackages);
        Assert.Contains("Microsoft.NET.Test.Sdk", assets.Packages);
        Assert.Contains("Microsoft.TestPlatform.TestHost", assets.Packages);
        Assert.Contains(CoreProject, assets.Projects);
    }

    [Theory]
    [InlineData("Ahwoo.BRUTAL.Core")]
    [InlineData("Ahwoo.BRUTAL.Numerics")]
    [InlineData("Ahwoo.BRUTAL.Maths")]
    [InlineData("Ahwoo.BRUTAL.Memory")]
    [InlineData("Ahwoo.BRUTAL.Collections")]
    [InlineData("Ahwoo.BRUTAL.Concurrency")]
    [InlineData("Ahwoo.BRUTAL.Logging")]
    [InlineData("ahwoo.brutal.core")]
    public void Check_AllowedBrutalPackage_GivesNoViolation(string package)
    {
        ProjectAssets assets = CreateAssets(directPackages: [package]);

        Assert.Empty(DependencyRules.Check(assets));
    }

    [Fact]
    public void Check_TransitiveBrutalPackageOfAnAllowedPackage_GivesNoViolation()
    {
        // Ahwoo.BRUTAL.Logging 2026.10.2 brings Ahwoo.BRUTAL.Strings.
        ProjectAssets assets = CreateAssets(
            directPackages: ["Ahwoo.BRUTAL.Logging"],
            transitivePackages: ["Ahwoo.BRUTAL.Strings", "Ahwoo.BRUTAL.Core"]);

        Assert.Empty(DependencyRules.Check(assets));
    }

    [Theory]
    [InlineData("Ahwoo.BRUTAL.Strings")]
    [InlineData("Ahwoo.BRUTAL.Jolt")]
    public void Check_DirectBrutalPackageNotInTheList_BreaksRule1(string package)
    {
        ProjectAssets assets = CreateAssets(directPackages: [package]);

        string violation = Assert.Single(DependencyRules.Check(assets));
        Assert.StartsWith("Rule 1:", violation);
        Assert.Contains(package, violation);
    }

    [Fact]
    public void Check_BrutalVulkanPackage_BreaksRules1And2()
    {
        ProjectAssets assets = CreateAssets(
            directPackages: ["Ahwoo.BRUTAL.Vulkan"],
            transitivePackages: ["Ahwoo.BRUTAL.Core", "Ahwoo.BRUTAL.Strings"]);

        IReadOnlyList<string> violations = DependencyRules.Check(assets);

        Assert.Collection(
            violations,
            violation => Assert.StartsWith("Rule 1: ElysianOutpost.Core references Ahwoo.BRUTAL.Vulkan.", violation),
            violation => Assert.StartsWith("Rule 2: ElysianOutpost.Core uses Ahwoo.BRUTAL.Vulkan (direct dependency).", violation));
    }

    [Theory]
    [InlineData("Ahwoo.BRUTAL.OpenGl")]
    [InlineData("Ahwoo.BRUTAL.Glfw")]
    [InlineData("Ahwoo.BRUTAL.Sdl.Image")]
    [InlineData("Ahwoo.BRUTAL.ImGui")]
    [InlineData("Ahwoo.BRUTAL.Vulkan.Vma")]
    [InlineData("Silk.NET.Vulkan")]
    [InlineData("ImGui.NET")]
    [InlineData("ppy.SDL3-CS")]
    public void Check_TransitiveForbiddenPackage_BreaksRule2(string package)
    {
        ProjectAssets assets = CreateAssets(directPackages: ["Some.Library"], transitivePackages: [package]);

        string violation = Assert.Single(DependencyRules.Check(assets));
        Assert.StartsWith($"Rule 2: ElysianOutpost.Core uses {package} (transitive dependency).", violation);
    }

    [Fact]
    public void Check_AppProject_BreaksRule3()
    {
        ProjectAssets assets = CreateAssets(projects: [new("ElysianOutpost.App", AppProjectFile)]);

        string violation = Assert.Single(DependencyRules.Check(assets));
        Assert.Equal("Rule 3: ElysianOutpost.Core uses the project ElysianOutpost.App.", violation);
    }

    // The assets file gives a project the name of its package ID or its assembly name. The rule uses the project file.
    [Fact]
    public void Check_AppProjectWithAnotherAssemblyName_BreaksRule3()
    {
        ProjectAssets assets = CreateAssets(projects: [new("ElysianOutpost", AppProjectFile)]);

        string violation = Assert.Single(DependencyRules.Check(assets));
        Assert.Equal("Rule 3: ElysianOutpost.Core uses the project ElysianOutpost.App.", violation);
    }

    [Theory]
    [InlineData("""{"libraries": {}}""", "project")]
    [InlineData("""{"project": [], "libraries": {}}""", "project")]
    [InlineData("""{"project": {"restore": {"projectName": "A"}, "frameworks": {}}}""", "libraries")]
    [InlineData("""{"project": {"restore": {"projectName": "A"}, "frameworks": {}}, "libraries": {"B/1.0.0": {"type": "project"}}}""", "path")]
    public void Parse_UnexpectedFormat_ThrowsInvalidDataException(string json, string property)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ProjectAssets.Parse(json));
        Assert.Contains($"\"{property}\"", exception.Message);
    }

    private static string FailureMessage(IReadOnlyList<string> violations) =>
        string.Join(
            Environment.NewLine,
            [
                "The core library breaks the dependency rules of docs/architecture.md:",
                .. violations,
                "To see why the core library uses a package, run this command at the root of the repository:",
                "dotnet nuget why src/ElysianOutpost.Core <package ID>",
            ]);

    // Makes a minimal assets file with the same structure as the files of the restore step.
    private static ProjectAssets CreateAssets(
        string[]? directPackages = null,
        string[]? transitivePackages = null,
        ProjectLibrary[]? projects = null)
    {
        var dependencies = new JsonObject();
        var libraries = new JsonObject();
        foreach (string package in directPackages ?? [])
        {
            dependencies[package] = new JsonObject { ["target"] = "Package", ["version"] = "[1.0.0, )" };
            libraries[$"{package}/1.0.0"] = new JsonObject { ["type"] = "package" };
        }

        foreach (string package in transitivePackages ?? [])
        {
            libraries[$"{package}/1.0.0"] = new JsonObject { ["type"] = "package" };
        }

        foreach (ProjectLibrary project in projects ?? [])
        {
            libraries[$"{project.Name}/1.0.0"] = new JsonObject { ["type"] = "project", ["path"] = project.Path };
        }

        var assets = new JsonObject
        {
            ["version"] = 3,
            ["libraries"] = libraries,
            ["project"] = new JsonObject
            {
                ["restore"] = new JsonObject { ["projectName"] = CoreProject },
                ["frameworks"] = new JsonObject
                {
                    ["net10.0"] = new JsonObject { ["dependencies"] = dependencies },
                },
            },
        };

        return ProjectAssets.Parse(assets.ToJsonString());
    }

    // A project in the "libraries" property of an assets file: its name in the key, and the path of its project file.
    private readonly record struct ProjectLibrary(string Name, string Path);
}
