using System.Text.Json;

namespace FlickerVoid.Editor.Persistence.Projects;

/// <summary>
/// Builds a game with <c>dotnet build</c>, after asking MSBuild for the built program and the projects the build builds (its
/// restore graph), so the build's output tells how far it has come (<see cref="BuildProgressReader"/>).
/// </summary>
public sealed class DotnetProjectBuilder : IProjectBuilder
{
    public string Build(string gameProject, IProgress<BuildProgress>? progress)
    {
        string name = Path.GetFileNameWithoutExtension(gameProject);
        progress?.Report(new BuildProgress(0, 0, $"Reading the projects {name} builds"));
        string tempFolder = Path.GetTempPath();
        string graphName = Path.GetRandomFileName();
        string graphFile = Path.Combine(tempFolder, graphName);
        try
        {
            string targetPath = DotnetCli.Run("msbuild", gameProject, "-nologo", "-t:GenerateRestoreGraphFile", $"-p:RestoreGraphOutputPath={graphFile}", "-getProperty:TargetPath");
            string program = targetPath.Trim();
            BuildProgressReader reader = ReadGraph(graphFile);
            string output = DotnetCli.Run(line => Report(reader, line, progress), "build", gameProject, "-nologo", "-v:minimal");
            if (program.Length == 0)
            {
                throw new ProjectBuildException($"{gameProject} built, but MSBuild named no program.\n{output}");
            }

            return program;
        }
        finally
        {
            File.Delete(graphFile);
        }
    }

    private static void Report(BuildProgressReader reader, string line, IProgress<BuildProgress>? progress)
    {
        BuildProgress now = reader.Read(line);
        progress?.Report(now);
    }

    /// <summary>A reader over the restore graph's projects, or over none when MSBuild wrote no graph it can read: the build then has no total.</summary>
    private static BuildProgressReader ReadGraph(string file)
    {
        try
        {
            string json = File.ReadAllText(file);
            return BuildProgressReader.FromRestoreGraph(json);
        }
        catch (Exception exception) when (exception is IOException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return new BuildProgressReader(new Dictionary<string, IReadOnlyList<string>>());
        }
    }
}
