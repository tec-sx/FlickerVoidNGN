using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Story.Knowledge;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Story.Tests.Knowledge;

/// <summary>Builds a game with story over knowledge files in a temporary content folder.</summary>
internal sealed class KnowledgeSystemTestsBuilder : IDisposable
{
    private readonly string _folder;
    private DataAssets? _data;

    public KnowledgeSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public KnowledgeSystem Knowledge { get; private set; } = null!;

    /// <summary>A context with the game's facts and services, as a condition or effect in content gets it.</summary>
    public ConditionContext Context => new(Game.Facts) { Services = Game.Services };

    /// <summary>A knowledge data asset whose id is <paramref name="id"/>, its other values given as JSON properties.</summary>
    public KnowledgeSystemTestsBuilder WithKnowledge(string id, string properties = "")
    {
        string separator = properties.Length > 0 ? "," : "";
        string fullPath = Path.Combine(_folder, id + DataAssets.AssetExtension);
        File.WriteAllText(fullPath, $$"""{ "type": "knowledge", "id": "{{id}}"{{separator}} {{properties}} }""");
        return this;
    }

    public KnowledgeSystemTestsBuilder Build()
    {
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Game.AddStory();
        Knowledge = Game.Services.GetService<KnowledgeSystem>()!;
        return this;
    }

    /// <summary>Reads a condition or effect written as JSON, with the types the game registered.</summary>
    public T Read<T>(string json)
        where T : class
    {
        JsonTypeInfo typeInfo = Game.Data.Options.GetTypeInfo(typeof(T));
        return (T)JsonSerializer.Deserialize(json, typeInfo)!;
    }

    public void Dispose()
    {
        Game.Dispose();
        _data?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
