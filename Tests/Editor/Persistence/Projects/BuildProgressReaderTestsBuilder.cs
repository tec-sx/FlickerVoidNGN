using System.Text.Json.Nodes;

using FlickerVoid.Editor.Persistence.Projects;

namespace FlickerVoid.Editor.Persistence.Tests.Projects;

/// <summary>Builds a reader of a build's output over the projects it builds, or the restore graph MSBuild writes of them.</summary>
internal sealed class BuildProgressReaderTestsBuilder
{
    private readonly Dictionary<string, IReadOnlyList<string>> _references = [];

    public BuildProgressReaderTestsBuilder WithProject(string name, params string[] references)
    {
        _references[name] = references;
        return this;
    }

    public BuildProgressReader Build() => new(_references);

    /// <summary>The projects as <c>dotnet msbuild -t:GenerateRestoreGraphFile</c> writes them: by path, with their references per framework.</summary>
    public string RestoreGraph()
    {
        JsonObject projects = [];
        foreach ((string name, IReadOnlyList<string> references) in _references)
        {
            JsonObject projectReferences = [];
            foreach (string reference in references)
            {
                string referencePath = ProjectPath(reference);
                projectReferences[referencePath] = new JsonObject { ["projectPath"] = referencePath };
            }

            JsonObject framework = new() { ["targetAlias"] = "net10.0", ["projectReferences"] = projectReferences };
            JsonObject frameworks = new() { ["net10.0"] = framework };
            JsonObject restore = new() { ["projectName"] = name, ["frameworks"] = frameworks };
            string path = ProjectPath(name);
            projects[path] = new JsonObject { ["restore"] = restore };
        }

        JsonObject graph = new() { ["format"] = 1, ["projects"] = projects };
        return graph.ToJsonString();
    }

    private static string ProjectPath(string name) => $"/source/{name}/{name}.csproj";
}
