using System.Text.Json.Nodes;

using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Flow.Tests.Graphs;

/// <summary>Builds a run of a graph written as JSON, read with the node types a game with flow registers, over a fresh fact database.</summary>
internal sealed class FlowRunTestsBuilder : IDisposable
{
    private readonly Game _game = CreateGame();

    public FactDatabase Facts { get; } = new(new FactSettings());

    public Game Game => _game;

    public FlowRun Build(string json)
    {
        JsonObject file = JsonNode.Parse(json)!.AsObject();
        FlowGraph graph = FlowGraphs.Read("Test.fvflow", file, _game.Data);
        ConditionContext conditions = new(Facts);
        return new FlowRun(graph, conditions);
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
