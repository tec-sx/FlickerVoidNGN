using Arch.Core;

using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Cameras.Tests.Scripting;

/// <summary>
/// Builds a playing game with a starting Hall shot at the origin and a Cellar shot ten metres right whose rail runs four metres
/// back in two seconds past a <c>Marker.Door</c> key, and applies camera effects or runs flow graphs of camera nodes against it.
/// </summary>
internal sealed class CameraScriptingTestsBuilder : IDisposable
{
    public CameraScriptingTestsBuilder()
    {
        Hall = Game.SpawnShot("Hall", 0, extra: """, "start": true""");
        string cellarKeys = """
            { "position": [0, 0, 0], "time": 0 }, { "position": [0, 0, 2], "time": 1, "marker": "Marker.Door" }, { "position": [0, 0, 4], "time": 2 }
            """;
        Cellar = Game.SpawnShot("Cellar", 10, cellarKeys);
        Game.Run(1 / 60f);
    }

    public CameraTestGame Game { get; } = new();

    public Entity Hall { get; }

    public Entity Cellar { get; }

    public void Apply(Effect effect)
    {
        EffectList effects = new() { Effects = [effect] };
        effects.Apply(Game.Context);
    }

    /// <summary>Starts a run of the nodes, each wired from the one before, after a start node; the run is finished when it ends.</summary>
    public FlowRun Run(params FlowNode[] nodes)
    {
        FlowGraph graph = new();
        Flow.Nodes.StartNode start = new() { Id = "start" };
        graph.Nodes.Add(start);
        string previous = start.Id;
        for (int index = 0; index < nodes.Length; index++)
        {
            FlowNode node = nodes[index];
            node.Id = $"node{index}";
            graph.Nodes.Add(node);
            graph.Links.Add(new FlowLink { From = previous, To = node.Id });
            previous = node.Id;
        }

        Flow.Nodes.FinishNode finish = new() { Id = "finish" };
        graph.Nodes.Add(finish);
        graph.Links.Add(new FlowLink { From = previous, To = finish.Id });
        FlowRun run = new(graph, Game.Context);
        run.Start();
        return run;
    }

    public void Dispose() => Game.Dispose();
}
