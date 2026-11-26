using System.Text.Json;

using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Rendering.Tests.Materials;

/// <summary>
/// Builds material files by path for a chain of instances to load its parents from, and writes them to a temporary content
/// folder to check them as a game with rendering does.
/// </summary>
internal sealed class MaterialChainTestsBuilder : IDisposable
{
    private readonly Dictionary<string, MaterialFile> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _contentFolder = Path.Combine(Path.GetTempPath(), $"materials-{Guid.NewGuid():N}");

    public MaterialChainTestsBuilder()
    {
        Directory.CreateDirectory(_contentFolder);
    }

    public List<string> Loaded { get; } = [];

    public MaterialChainTestsBuilder With(string path, string json)
    {
        _files.Add(path, Material(json));
        string fullPath = Path.Combine(_contentFolder, path);
        File.WriteAllText(fullPath, json);
        return this;
    }

    /// <summary>What the content check of a game with rendering finds wrong in the files written.</summary>
    public IReadOnlyList<ContentProblem> Check()
    {
        using Game game = new(NullLoggerFactory.Instance);
        game.AddRendering();
        return ContentCheck.Run(game, _contentFolder);
    }

    public void Dispose()
    {
        Directory.Delete(_contentFolder, recursive: true);
    }

    public static MaterialFile Material(string json)
    {
        return JsonSerializer.Deserialize(json, RenderingJsonContext.Default.MaterialFile)!;
    }

    public MaterialFile Load(string path)
    {
        Loaded.Add(path);
        return _files[path];
    }
}
