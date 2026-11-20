using System.Text.Json.Nodes;

using FlickerVoid.Editor.Core.Flows;
using FlickerVoid.Engine.Flow;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Core.Tests.Flows;

/// <summary>Builds the canvas of a graph written as JSON, with the node types of a game with flow, counting the edits it reports.</summary>
internal sealed class FlowGraphViewModelTestsBuilder : IDisposable
{
    private readonly Game _game = CreateGame();

    public FlowGraph Graph { get; private set; } = new();

    public int Edits { get; private set; }

    public FlowGraphViewModel Build(string json)
    {
        JsonObject file = JsonNode.Parse(json)!.AsObject();
        Graph = FlowGraphs.Read("Test.fvflow", file, _game.Data);
        return new FlowGraphViewModel("Test", Graph, _game.Data, () => Edits++);
    }

    public void Dispose()
    {
        _game.Dispose();
    }

    private static Game CreateGame()
    {
        Game game = new(NullLoggerFactory.Instance);
        game.AddFlow();
        return game;
    }
}
