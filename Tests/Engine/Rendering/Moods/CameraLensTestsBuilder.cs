using Arch.Core;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Rendering.Tests.Moods;

/// <summary>
/// Writes lens files to a temporary content folder to load them back or check them as a game with rendering does, and
/// builds worlds with a main camera to find the lens it names.
/// </summary>
internal sealed class CameraLensTestsBuilder : IDisposable
{
    private readonly string _contentFolder = Path.Combine(Path.GetTempPath(), $"lenses-{Guid.NewGuid():N}");
    private readonly World _world = World.Create();

    public CameraLensTestsBuilder()
    {
        Directory.CreateDirectory(_contentFolder);
    }

    public World World => _world;

    public CameraLensTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_contentFolder, path);
        File.WriteAllText(fullPath, json);
        return this;
    }

    public CameraLensTestsBuilder WithMainCamera(string lens)
    {
        MainCamera main = new() { Lens = lens };
        _world.Create(new Camera(), new Transform(), main);
        return this;
    }

    public CameraLens Load(string path)
    {
        ContentManager content = new(_contentFolder);
        return content.Load(path, RenderingJsonContext.Default.CameraLens);
    }

    /// <summary>What the content check of a game with rendering finds wrong in the files written.</summary>
    public IReadOnlyList<ContentProblem> Check()
    {
        using Game game = new(NullLoggerFactory.Instance);
        game.AddRendering();
        return ContentCheck.Run(game, _contentFolder);
    }

    /// <summary>The schema a game with rendering writes for lens files.</summary>
    public string WriteSchema()
    {
        using Game game = new(NullLoggerFactory.Instance);
        game.AddRendering();
        game.Schemas.Write(_contentFolder);
        string path = Path.Combine(_contentFolder, ContentSchemas.FolderName, "lens.schema.json");
        return File.ReadAllText(path);
    }

    public void Dispose()
    {
        World.Destroy(_world);
        Directory.Delete(_contentFolder, recursive: true);
    }
}
