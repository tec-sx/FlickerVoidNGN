using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Flow.Tests.Graphs;

/// <summary>Builds the flow graphs under test over files in a temporary content folder, with the node types of a game with flow.</summary>
internal sealed class FlowGraphsTestsBuilder : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("flow-").FullName;
    private readonly Game _game = CreateGame();

    public Game Game => _game;

    public FlowGraphsTestsBuilder WithFile(string path, string json)
    {
        string full = Path.Combine(_root, path);
        string directory = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(full, json);
        return this;
    }

    /// <summary>Writes the game's schemas as a development run does and returns their folder.</summary>
    public string WriteSchemas()
    {
        _game.Schemas.Write(_root);
        return Path.Combine(_root, ContentSchemas.FolderName);
    }

    public FlowGraphs Build()
    {
        ContentManager content = new(_root);
        return new FlowGraphs(content, _game.Data);
    }

    public void Dispose()
    {
        _game.Dispose();
        Directory.Delete(_root, true);
    }

    private static Game CreateGame()
    {
        Game game = new(NullLoggerFactory.Instance);
        game.AddFlow();
        return game;
    }
}
