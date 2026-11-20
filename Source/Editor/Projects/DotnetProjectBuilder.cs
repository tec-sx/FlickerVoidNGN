namespace FlickerVoid.Engine.Editor.Projects;

/// <summary>Builds a game with <c>dotnet build</c>, then asks MSBuild for the built program.</summary>
public sealed class DotnetProjectBuilder : IProjectBuilder
{
    public string Build(string gameProject)
    {
        string output = DotnetCli.Run("build", gameProject, "-nologo", "-v:minimal");
        string targetPath = DotnetCli.Run("msbuild", gameProject, "-nologo", "-getProperty:TargetPath");
        string program = targetPath.Trim();
        if (program.Length == 0)
        {
            throw new ProjectBuildException($"{gameProject} built, but MSBuild named no program.\n{output}");
        }

        return program;
    }
}
