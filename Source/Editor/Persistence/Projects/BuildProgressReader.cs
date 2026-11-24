using System.Text.Json;

namespace FlickerVoid.Editor.Persistence.Projects;

/// <summary>
/// Follows a <c>dotnet build</c> at minimal verbosity line by line. MSBuild prints <c>Name -> output</c> as each project finishes,
/// so the build has come as far as the projects that printed it, and the projects whose references all have are building now;
/// other lines (restore, content, errors) are shown as they are, until every project is built.
/// </summary>
public sealed class BuildProgressReader
{
    private const string Arrow = " -> ";

    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _references;
    private readonly HashSet<string> _built = [];

    /// <param name="references">The projects the build builds, by name, each with the names of the projects it references.</param>
    public BuildProgressReader(IReadOnlyDictionary<string, IReadOnlyList<string>> references)
    {
        _references = references;
        Progress = new BuildProgress(0, references.Count, "");
    }

    /// <summary>How far the build has come by the lines read so far.</summary>
    public BuildProgress Progress { get; private set; }

    private bool IsDone => _references.Count > 0 && _built.Count == _references.Count;

    /// <summary>Reads the projects and their references from the restore graph <c>dotnet msbuild -t:GenerateRestoreGraphFile</c> writes.</summary>
    public static BuildProgressReader FromRestoreGraph(string json)
    {
        using JsonDocument graph = JsonDocument.Parse(json);
        JsonElement projects = graph.RootElement.GetProperty("projects");
        Dictionary<string, IReadOnlyList<string>> references = [];
        foreach (JsonProperty project in projects.EnumerateObject())
        {
            string name = Path.GetFileNameWithoutExtension(project.Name);
            references[name] = ReadReferences(project.Value);
        }

        return new BuildProgressReader(references);
    }

    /// <summary>Takes the build's next line and returns how far the build has come.</summary>
    public BuildProgress Read(string line)
    {
        string text = line.Trim();
        if (text.Length == 0 || IsDone)
        {
            return Progress;
        }

        int arrow = text.IndexOf(Arrow, StringComparison.Ordinal);
        string project = "";
        if (arrow > 0)
        {
            project = text[..arrow];
        }

        if (_references.ContainsKey(project))
        {
            _built.Add(project);
            string status = DescribeBuilding(project);
            Progress = new BuildProgress(_built.Count, _references.Count, status);
        }
        else
        {
            Progress = Progress with { Status = text };
        }

        return Progress;
    }

    /// <summary>The projects a project references, by name, over all the frameworks it targets.</summary>
    private static List<string> ReadReferences(JsonElement project)
    {
        List<string> names = [];
        JsonElement restore = project.GetProperty("restore");
        JsonElement frameworks = restore.GetProperty("frameworks");
        foreach (JsonProperty framework in frameworks.EnumerateObject())
        {
            if (!framework.Value.TryGetProperty("projectReferences", out JsonElement projectReferences))
            {
                continue;
            }

            foreach (JsonProperty reference in projectReferences.EnumerateObject())
            {
                string name = Path.GetFileNameWithoutExtension(reference.Name);
                if (!names.Contains(name))
                {
                    names.Add(name);
                }
            }
        }

        return names;
    }

    /// <summary>The projects whose references are all built, or the one built last once none is left.</summary>
    private string DescribeBuilding(string builtLast)
    {
        List<string> building = [];
        foreach ((string name, IReadOnlyList<string> references) in _references)
        {
            if (!_built.Contains(name) && references.All(IsBuilt))
            {
                building.Add(name);
            }
        }

        if (building.Count == 0)
        {
            return $"Built {builtLast}";
        }

        building.Sort(StringComparer.Ordinal);
        string names = string.Join(", ", building);
        return $"Building {names}";
    }

    /// <summary>Whether the project is built; one the build doesn't list counts as built, as nothing waits for it.</summary>
    private bool IsBuilt(string project) => _built.Contains(project) || !_references.ContainsKey(project);
}
