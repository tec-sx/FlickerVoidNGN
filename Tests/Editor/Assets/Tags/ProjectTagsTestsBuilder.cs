using FlickerVoid.Editor.Assets.Tags;
using FlickerVoid.Editor.Core.Tags;
using FlickerVoid.Engine.Cameras.Serialization;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Assets.Tests.Tags;

/// <summary>A content folder of a scene whose trigger areas hold tags, a game that reads them, and the project's tags over both.</summary>
internal sealed class ProjectTagsTestsBuilder : IDisposable
{
    public const string Scene = """
        { "entities": [
          { "name": "Inn Room", "components": { "triggerArea": { "requiredTags": ["Character.Fox", "Music.Drums"] } } },
          { "name": "Cellar", "components": { "triggerArea": { "requiredTags": ["Music.Drums.Cellar"] } } }
        ] }
        """;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly Game _game = new(NullLoggerFactory.Instance);
    private string? _busy;

    public ProjectTagsTestsBuilder()
    {
        Directory.CreateDirectory(_folder);
        _game.Spawner.Register("triggerArea", CamerasJsonContext.Default.TriggerArea);
        WithFile("Main.fvscene", Scene);
        WithFile("Engine/Skip.fvscene", Scene);
    }

    public TagCatalog Catalog { get; } = new();

    public List<string> Rewritten { get; } = [];

    public ProjectTagsTestsBuilder WithFile(string path, string text)
    {
        string fullPath = Path.Combine(_folder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, text);
        return this;
    }

    /// <summary>Makes the project say it can't rewrite files now, as with unsaved work.</summary>
    public ProjectTagsTestsBuilder Busy(string why)
    {
        _busy = why;
        return this;
    }

    public ProjectTags Build()
    {
        UnsavedContent content = new(new ContentManager(_folder));
        ProjectTags tags = new(content, () => _game, () => _busy);
        tags.Rewritten += paths => Rewritten.AddRange(paths);
        tags.Load(Catalog);
        return tags;
    }

    public string ReadFile(string path) => File.ReadAllText(Path.Combine(_folder, path));

    public bool HasFile(string path) => File.Exists(Path.Combine(_folder, path));

    public void Dispose()
    {
        _game.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
