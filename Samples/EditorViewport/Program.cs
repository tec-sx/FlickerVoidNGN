using Avalonia;

using FlickerVoid.Samples.EditorViewport;

// The stage 3 spike: the engine renders into an Avalonia window through Avalonia's GPU interop. Avalonia and the engine both run on
// Vulkan, on Windows and Linux, and share one texture and two semaphores. "--validation" turns on the Vulkan validation layer.
bool validation = args.Contains("--validation");
AppBuilder.Configure(() => new App(validation))
    .UsePlatformDetect()
    .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Vulkan] })
    .With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Vulkan] })
    .StartWithClassicDesktopLifetime(args);
return 0;
