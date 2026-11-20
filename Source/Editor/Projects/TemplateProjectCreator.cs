namespace FlickerVoid.Engine.Editor.Projects;

/// <summary>
/// Creates projects from the engine's <c>dotnet new flickervoid</c> template, installing it from the engine folder
/// <see cref="DotnetCli.Engine"/> finds, where builds find the engine too.
/// </summary>
public sealed class TemplateProjectCreator : IProjectCreator
{
    public string Create(string name, string folder)
    {
        string engine = DotnetCli.Engine;
        string template = Path.Combine(engine, DotnetCli.TemplateFolder);
        DotnetCli.Run("new", "install", template, "--force");
        string output = Path.Combine(folder, name);
        DotnetCli.Run("new", "flickervoid", "-n", name, "--engine", engine, "-o", output);
        return Path.Combine(output, name + EditorProject.Extension);
    }
}
