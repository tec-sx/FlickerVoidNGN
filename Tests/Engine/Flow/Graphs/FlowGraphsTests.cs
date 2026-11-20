using System.Text.Json.Nodes;

using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Flow.Nodes;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Flow.Tests.Graphs;

public sealed class FlowGraphsTests : IDisposable
{
    private const string Cellar = """
        {
          "$schema": "../.schemas/flow.schema.json",
          // Opens the cellar once the player has the key.
          "nodes": [
            { "$type": "start", "id": "start", "x": 0, "y": 0 },
            { "$type": "waitForConditions", "id": "wait", "x": 200, "conditions": { "conditions": [ { "$type": "fact", "fact": "Item.Key.Cellar" } ] } },
            { "$type": "finish", "id": "end", "x": 400 },
          ],
          "links": [ { "from": "start", "to": "wait" }, { "from": "wait", "to": "end" } ],
        }
        """;

    private readonly FlowGraphsTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void LoadsAGraphFileWithItsNodesAndConditions()
    {
        FlowGraphs graphs = _builder.WithFile("Quests/Cellar.fvflow", Cellar).Build();

        FlowGraph graph = graphs.Load("Quests/Cellar.fvflow");

        graph.Nodes.Should().HaveCount(3);
        WaitForConditionsNode wait = graph.Nodes[1].Should().BeOfType<WaitForConditionsNode>().Subject;
        wait.X.Should().Be(200);
        FactCondition condition = wait.Conditions.Conditions.Single().Should().BeOfType<FactCondition>().Subject;
        condition.Fact.Should().Be(new Tag("Item.Key.Cellar"));
        graph.Links[1].Output.Should().Be(FlowNode.DefaultOutput);
    }

    [Fact]
    public void StartsARunOfAGraphFile()
    {
        FlowGraphs graphs = _builder.WithFile("Quests/Cellar.fvflow", Cellar).Build();
        FactDatabase facts = new(new FactSettings());
        ConditionContext conditions = new(facts);

        FlowRun run = graphs.Start("Quests/Cellar.fvflow", conditions);
        facts.SetFact(new Tag("Item.Key.Cellar"), 1);

        run.IsRunning.Should().BeFalse("the key let the run reach its finish node");
    }

    [Fact]
    public void ReadsEachLoadFresh()
    {
        FlowGraphs graphs = _builder.WithFile("Quests/Cellar.fvflow", Cellar).Build();
        graphs.Load("Quests/Cellar.fvflow");
        _builder.WithFile("Quests/Cellar.fvflow", """{ "nodes": [ { "$type": "start", "id": "start" } ] }""");

        FlowGraph graph = graphs.Load("Quests/Cellar.fvflow");

        graph.Nodes.Should().ContainSingle();
    }

    [Fact]
    public void RefusesAnUnknownNodeType()
    {
        FlowGraphs graphs = _builder.WithFile("Quests/Bad.fvflow", """{ "nodes": [ { "$type": "teleport", "id": "a" } ] }""").Build();

        Action load = () => graphs.Load("Quests/Bad.fvflow");

        load.Should().Throw<InvalidDataException>().WithMessage("*Quests/Bad.fvflow*");
    }

    [Fact]
    public void AddsTheFlowSchemaWithEveryNodeType()
    {
        JsonObject schema = FlowGraphs.GetSchema(_builder.Game.Data);

        string text = schema.ToJsonString();
        text.Should().Contain("waitForConditions").And.Contain("applyEffects");
        string folder = _builder.WriteSchemas();
        File.Exists(Path.Combine(folder, "flow.schema.json")).Should().BeTrue();
    }
}
