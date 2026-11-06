// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Tests;

/// <summary>
/// Gives the paths of files in the repository, for the tests that read them.
/// </summary>
internal static class RepositoryPaths
{
    // The tests need the core library at this location. The name of the solution file can change, for example to .slnx.
    private const string CoreProjectFile = "src/ElysianOutpost.Core/ElysianOutpost.Core.csproj";

    /// <summary>
    /// Gets the root folder of the repository: the folder that contains <c>src/ElysianOutpost.Core/ElysianOutpost.Core.csproj</c>.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">No parent folder of the test assembly contains the project file of the core library.</exception>
    public static string Root => FindRoot();

    /// <summary>
    /// Gets the assets file of the core library. The restore step writes this file.
    /// </summary>
    public static string CoreAssetsFile => Path.Combine(Root, "src", "ElysianOutpost.Core", "obj", "project.assets.json");

    /// <summary>
    /// Gets the assets file of this test project. The restore step writes this file.
    /// </summary>
    public static string TestsAssetsFile => Path.Combine(Root, "tests", "ElysianOutpost.Core.Tests", "obj", "project.assets.json");

    private static string FindRoot()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, CoreProjectFile)))
            {
                return folder.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"The folder {AppContext.BaseDirectory} and its parent folders do not contain {CoreProjectFile}.");
    }
}
