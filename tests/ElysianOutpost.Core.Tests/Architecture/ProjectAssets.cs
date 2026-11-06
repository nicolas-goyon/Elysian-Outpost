// SPDX-License-Identifier: MPL-2.0

using System.Text;
using System.Text.Json;

namespace ElysianOutpost.Core.Tests.Architecture;

/// <summary>
/// Gives the dependencies of a project from its assets file (<c>obj/project.assets.json</c>).
/// The restore step writes this file.
/// </summary>
internal sealed class ProjectAssets
{
    private ProjectAssets(
        string projectName,
        IReadOnlySet<string> directPackages,
        IReadOnlySet<string> packages,
        IReadOnlySet<string> projects)
    {
        ProjectName = projectName;
        DirectPackages = directPackages;
        Packages = packages;
        Projects = projects;
    }

    /// <summary>
    /// Gets the name of the project.
    /// </summary>
    public string ProjectName { get; }

    /// <summary>
    /// Gets the IDs of the packages that the project references, for all the target frameworks.
    /// </summary>
    public IReadOnlySet<string> DirectPackages { get; }

    /// <summary>
    /// Gets the IDs of all the packages of the project: the direct and the transitive dependencies.
    /// </summary>
    public IReadOnlySet<string> Packages { get; }

    /// <summary>
    /// Gets all the projects that the project uses: the direct and the transitive dependencies.
    /// Each item is the name of a project file without its extension, for example <c>ElysianOutpost.App</c>.
    /// </summary>
    public IReadOnlySet<string> Projects { get; }

    /// <summary>
    /// Reads an assets file.
    /// </summary>
    /// <param name="path">The path of the assets file.</param>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="JsonException">The file does not contain valid JSON.</exception>
    /// <exception cref="InvalidDataException">The content does not have the expected format.</exception>
    public static ProjectAssets Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The assets file {path} does not exist. Run \"dotnet restore\" first.", path);
        }

        return Parse(File.ReadAllText(path));
    }

    /// <summary>
    /// Reads the content of an assets file.
    /// </summary>
    /// <param name="json">The content of the assets file.</param>
    /// <exception cref="JsonException">The content is not valid JSON.</exception>
    /// <exception cref="InvalidDataException">The content does not have the expected format.</exception>
    public static ProjectAssets Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement project = GetRequired(root, "project"u8, JsonValueKind.Object);
        JsonElement restore = GetRequired(project, "restore"u8, JsonValueKind.Object);
        string projectName = GetRequired(restore, "projectName"u8, JsonValueKind.String).GetString()!;

        var directPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty framework in GetRequired(project, "frameworks"u8, JsonValueKind.Object).EnumerateObject())
        {
            // A framework without package references has no "dependencies" property.
            if (GetOptional(framework.Value, "dependencies"u8, JsonValueKind.Object) is JsonElement dependencies)
            {
                foreach (JsonProperty dependency in dependencies.EnumerateObject())
                {
                    directPackages.Add(dependency.Name);
                }
            }
        }

        // The "libraries" property contains all the dependencies. Each key has the format "<name>/<version>".
        // The name of a project can come from its package ID or its assembly name. Thus the parser uses the project file.
        var packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty library in GetRequired(root, "libraries"u8, JsonValueKind.Object).EnumerateObject())
        {
            string type = GetRequired(library.Value, "type"u8, JsonValueKind.String).GetString()!;
            if (type == "package")
            {
                packages.Add(library.Name.Split('/')[0]);
            }
            else if (type == "project")
            {
                string projectFile = GetRequired(library.Value, "path"u8, JsonValueKind.String).GetString()!;
                projects.Add(Path.GetFileNameWithoutExtension(projectFile.Replace('\\', '/')));
            }
            else
            {
                throw new InvalidDataException($"The library {library.Name} has an unknown type: {type}.");
            }
        }

        return new ProjectAssets(projectName, directPackages, packages, projects);
    }

    private static JsonElement GetRequired(JsonElement element, ReadOnlySpan<byte> utf8Name, JsonValueKind kind) =>
        GetOptional(element, utf8Name, kind)
        ?? throw new InvalidDataException(
            $"The assets file has no property \"{Encoding.UTF8.GetString(utf8Name)}\" at the expected location.");

    private static JsonElement? GetOptional(JsonElement element, ReadOnlySpan<byte> utf8Name, JsonValueKind kind)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(utf8Name, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind != kind)
        {
            throw new InvalidDataException(
                $"The property \"{Encoding.UTF8.GetString(utf8Name)}\" of the assets file has the type {value.ValueKind}, not {kind}.");
        }

        return value;
    }
}
