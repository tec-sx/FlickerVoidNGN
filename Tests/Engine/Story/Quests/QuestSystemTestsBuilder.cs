using FlickerVoid.Engine.Flow;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Story.Quests;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Story.Tests.Quests;

/// <summary>Builds a running game with flow and story over quest and flow files in a temporary content folder.</summary>
internal sealed class QuestSystemTestsBuilder : IDisposable
{
    private readonly string _folder;
    private DataAssets? _data;

    public QuestSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public QuestSystem Quests { get; private set; } = null!;

    public SaveSystem Saves { get; private set; } = null!;

    public QuestSystemTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string? folder = Path.GetDirectoryName(fullPath);
        Directory.CreateDirectory(folder!);
        File.WriteAllText(fullPath, json);
        return this;
    }

    /// <summary>A quest data asset whose id is <paramref name="id"/>, its other values given as JSON properties.</summary>
    public QuestSystemTestsBuilder WithQuest(string id, string properties = "")
    {
        string separator = properties.Length > 0 ? "," : "";
        return WithFile($"Quests/{id}.fvdata", $$"""{ "type": "quest", "id": "{{id}}"{{separator}} {{properties}} }""");
    }


    /// <summary>Adds flow, story and the content services, then runs the first step, which starts the quests.</summary>
    public QuestSystemTestsBuilder Build()
    {
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Game.AddFlow();
        Game.Services.AddService(new FlowGraphs(content, Game.Data));
        SaveSettings saves = new() { Folder = Path.Combine(_folder, ".saves") };
        Saves = Game.AddSaves(saves);
        Game.AddStory();
        Quests = Game.Services.GetService<QuestSystem>()!;
        Game.Tick(TimeSpan.Zero);
        return this;
    }

    public List<T> Collect<T>()
        where T : struct
    {
        List<T> sent = [];
        Game.Events.Handle((in T value) => sent.Add(value));
        return sent;
    }

    public void Dispose()
    {
        Game.Dispose();
        _data?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
