using Arch.Core;

using FlickerVoid.Engine.Audio.Scripting;
using FlickerVoid.Engine.Audio.Tests.Sounds;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Audio.Tests.Scripting;

public sealed class AudioScriptingTests : IDisposable
{
    private readonly AudioSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TheEffectPlaysASoundWhereItsTargetStands()
    {
        _builder.SpawnListener();
        Entity door = _builder.Spawn("""{ "name": "Door", "components": { "transform": { "position": [3, 0, 0] } } }""");
        string sound = _builder.WithSound("Creak", string.Empty, 0.5f);
        PlaySoundEffect effect = new() { Sound = sound };
        WorldEntityView target = new(_builder.Game.World, door);
        ConditionContext context = _builder.Context with { Target = target };

        effect.Apply(context);

        (float left, float right) = _builder.Hear();
        right.Should().BeGreaterThan(left * 2);
    }

    [Fact]
    public void TheEffectPlaysASoundAsItIsWithoutATarget()
    {
        _builder.SpawnListener();
        string sound = _builder.WithSound("Chime", string.Empty, 0.5f);
        PlaySoundEffect effect = new() { Sound = sound };

        effect.Apply(_builder.Context);

        (float left, float right) = _builder.Hear();
        left.Should().BeApproximately(0.5f, 0.001f);
        right.Should().BeApproximately(0.5f, 0.001f);
    }

    [Fact]
    public void TheNodePlaysASoundAndPassesOn()
    {
        string sound = _builder.WithSound("Chime", "\"spatial\": false", 0.5f);
        PlaySoundNode node = new() { Sound = sound };
        FlowGraph graph = new();
        Flow.Nodes.StartNode start = new() { Id = "start" };
        Flow.Nodes.FinishNode finish = new() { Id = "finish" };
        node.Id = "play";
        graph.Nodes.Add(start);
        graph.Nodes.Add(node);
        graph.Nodes.Add(finish);
        FlowLink toPlay = new() { From = start.Id, To = node.Id };
        FlowLink toFinish = new() { From = node.Id, To = finish.Id };
        graph.Links.Add(toPlay);
        graph.Links.Add(toFinish);
        FlowRun run = new(graph, _builder.Context);

        run.Start();

        (float left, _) = _builder.Hear();
        left.Should().BeApproximately(0.5f, 0.001f);
        run.IsRunning.Should().BeFalse();
    }
}
