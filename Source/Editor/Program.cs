using Avalonia;

using FlickerVoid.Engine.Editor;
using FlickerVoid.Engine.Editor.Shell;

// The FlickerVoid editor. Usage: FlickerVoid.Engine.Editor [--content <built content folder> [--scene <scene file in it>]] [--validation].
// Avalonia and the engine both run on Vulkan and share the viewport's texture; --validation turns on the Vulkan validation layer.
EditorOptions? options = EditorOptions.Parse(args, out string? error);
if (options is null)
{
    Console.Error.WriteLine(error);
    return 2;
}

AppBuilder.Configure(() => new App { Options = options })
    .UsePlatformDetect()
    .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Vulkan] })
    .With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Vulkan] })
    .StartWithClassicDesktopLifetime(args);
return 0;
