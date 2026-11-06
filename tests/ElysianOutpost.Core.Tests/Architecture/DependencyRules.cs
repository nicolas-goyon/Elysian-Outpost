// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Tests.Architecture;

/// <summary>
/// The dependency rules of the core library. The document <c>docs/architecture.md</c> gives the rules and their reasons.
/// Change the document and this class in the same pull request.
/// </summary>
internal static class DependencyRules
{
    private const string BrutalPrefix = "Ahwoo.BRUTAL.";

    /// <summary>
    /// Gets the BRUTAL packages that the core library can reference directly (rule 1).
    /// </summary>
    public static IReadOnlyList<string> AllowedBrutalPackages { get; } =
    [
        "Ahwoo.BRUTAL.Core",
        "Ahwoo.BRUTAL.Numerics",
        "Ahwoo.BRUTAL.Maths",
        "Ahwoo.BRUTAL.Memory",
        "Ahwoo.BRUTAL.Collections",
        "Ahwoo.BRUTAL.Concurrency",
        "Ahwoo.BRUTAL.Logging",
    ];

    /// <summary>
    /// Gets the names that the ID of a package must not contain, in any letter case (rule 2).
    /// </summary>
    public static IReadOnlyList<string> ForbiddenPackageNames { get; } = ["Vulkan", "OpenGL", "GLFW", "SDL", "ImGui"];

    /// <summary>
    /// Gets the projects that the core library must not use (rule 3).
    /// Each item is the name of a project file without its extension.
    /// </summary>
    public static IReadOnlyList<string> ForbiddenProjects { get; } = ["ElysianOutpost.App"];

    /// <summary>
    /// Checks the dependencies of a project against the rules.
    /// </summary>
    /// <param name="assets">The dependencies of the project.</param>
    /// <returns>One message for each broken rule. The list is empty if the project follows all the rules.</returns>
    public static IReadOnlyList<string> Check(ProjectAssets assets)
    {
        var violations = new List<string>();

        foreach (string package in Sorted(assets.DirectPackages))
        {
            if (package.StartsWith(BrutalPrefix, StringComparison.OrdinalIgnoreCase)
                && !AllowedBrutalPackages.Contains(package, StringComparer.OrdinalIgnoreCase))
            {
                violations.Add(
                    $"Rule 1: {assets.ProjectName} references {package}. "
                    + $"It can reference only these BRUTAL packages: {string.Join(", ", AllowedBrutalPackages)}.");
            }
        }

        foreach (string package in Sorted(assets.Packages))
        {
            string? name = ForbiddenPackageNames.FirstOrDefault(
                forbidden => package.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
            if (name is not null)
            {
                string kind = assets.DirectPackages.Contains(package) ? "direct" : "transitive";
                violations.Add(
                    $"Rule 2: {assets.ProjectName} uses {package} ({kind} dependency). "
                    + $"The package ID contains \"{name}\".");
            }
        }

        foreach (string project in Sorted(assets.Projects))
        {
            if (ForbiddenProjects.Contains(project, StringComparer.OrdinalIgnoreCase))
            {
                violations.Add($"Rule 3: {assets.ProjectName} uses the project {project}.");
            }
        }

        return violations;
    }

    private static IEnumerable<string> Sorted(IEnumerable<string> names) => names.Order(StringComparer.OrdinalIgnoreCase);
}
