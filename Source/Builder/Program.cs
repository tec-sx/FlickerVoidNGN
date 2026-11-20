using System.Diagnostics;
using System.Globalization;

using FlickerVoid.Engine.Builder;
using FlickerVoid.Engine.Builder.Importing;
using FlickerVoid.Engine.Builder.Models;
using FlickerVoid.Engine.Builder.Shaders;
using FlickerVoid.Engine.Builder.Textures;
using FlickerVoid.Engine.Builder.Watching;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Shaders;

// Builds a content folder: FlickerVoid.Engine.Builder <source folder> <output folder>. Exits with 1 when a file fails.
// With --watch <manifest> [--parent <process id>] it keeps the manifest's folders built while they change, until the parent exits.
const string Usage = "Usage: FlickerVoid.Engine.Builder <source folder> <output folder>, or --watch <manifest> [--parent <process id>]";
bool watch = args.Length is 2 or 4 && args[0] == "--watch";
if (args.Length != 2 && !watch)
{
    Console.Error.WriteLine(Usage);
    return 2;
}

using DxcShaderCompiler compiler = new();
ShaderImporter shaders = new(compiler);
SkipImporter shaderSources = new(".hlsl", ".hlsli");
TextureImporter textures = new();
ModelImporter models = new();
IAssetImporter[] importers = [shaders, shaderSources, textures, models];
AssetBuilder builder = new(importers);
if (watch)
{
    Process? parent = null;
    if (args.Length == 4)
    {
        int parentId = int.Parse(args[3], CultureInfo.InvariantCulture);
        parent = Process.GetProcessById(parentId);
    }

    using ContentWatchLock? watchLock = ContentWatchLock.TryTake(args[1]);
    if (watchLock is null)
    {
        Console.WriteLine("Another builder already watches these content folders.");
        return 0;
    }

    ContentBuildManifest manifest = ContentBuildManifest.Read(args[1]);
    using ContentWatchBuilder watcher = new(builder, manifest.Folders);
    Console.WriteLine($"Watching {manifest.Folders.Count} content folders.");
    while (parent is null || !parent.HasExited)
    {
        IReadOnlyList<BuildReport> reports = watcher.BuildSettled();
        foreach (BuildReport watched in reports)
        {
            Print(watched);
        }

        Thread.Sleep(20);
    }

    return 0;
}

BuildReport report = builder.Build(args[0], args[1]);
Print(report);
if (report.Failed.Count > 0)
{
    return 1;
}

return 0;

static void Print(BuildReport report)
{
    foreach (BuildFailure failure in report.Failed)
    {
        Console.Error.WriteLine($"{failure.Path}: error: {failure.Message}");
    }

    Console.WriteLine($"Content: {report.Built.Count} built, {report.Skipped.Count} up to date, {report.Removed.Count} removed, {report.Failed.Count} failed.");
}
