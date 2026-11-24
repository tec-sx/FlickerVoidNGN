using Avalonia;

using FlickerVoid.Editor;
using FlickerVoid.Editor.Persistence.Projects;
using FlickerVoid.Editor.Projects;

// The FlickerVoid editor. Usage: FlickerVoid.Editor [<project.fvproject> | --content <built content folder>] [--scene <scene>] [--validation].
// A project is built first (its game builds its content), then the editor opens the built content and its startup scene.
// Avalonia and the engine both run on Vulkan and share the viewport's texture; --validation turns on the Vulkan validation layer.
EditorOptions? options = EditorOptions.Parse(args, out string? error);
if (options is null)
{
    Console.Error.WriteLine(error);
    return 2;
}

if (options.Project is not null)
{
    Console.WriteLine($"Building {options.Project}...");
    try
    {
        DotnetProjectBuilder builder = new();
        EditorProject project = EditorProject.Open(options.Project, builder, null);
        string recentFile = RecentProjects.GetDefaultFile();
        RecentProjects recent = new(recentFile);
        recent.Add(options.Project);
        string? scene = options.Scene ?? project.StartupScene;
        options = options with { Content = project.ContentFolder, Scene = scene, Game = project.Program, GameProject = project.Game, Solution = project.Solution, Code = project.CodeFolder };
    }
    catch (Exception exception) when (exception is InvalidDataException or ProjectBuildException or IOException)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

AppBuilder.Configure(() => new App { Options = options })
    .UsePlatformDetect()
    .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Vulkan] })
    .With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Vulkan] })
    .StartWithClassicDesktopLifetime(args);
return 0;
