namespace FlickerVoid.Engine.Editor.Projects;

/// <summary>Builds a game with <c>dotnet build</c>, then asks MSBuild for its output folder.</summary>
public sealed class DotnetProjectBuilder : IProjectBuilder
{
    public string Build(string gameProject)
    {
        string output = DotnetCli.Run("build", gameProject, "-nologo", "-v:minimal");
        string targetDir = DotnetCli.Run("msbuild", gameProject, "-nologo", "-getProperty:TargetDir");
        string folder = targetDir.Trim();
        if (folder.Length == 0)
        {
            throw new ProjectBuildException($"{gameProject} built, but MSBuild named no output folder.\n{output}");
        }

        return folder;
    }
}
