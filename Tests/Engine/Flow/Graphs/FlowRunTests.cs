using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Flow.Nodes;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Flow.Tests.Graphs;

public sealed class FlowRunTests : IDisposable
{
    private static readonly Tag Opened = new("Quest.Cellar.Opened");
    private static readonly Tag HasKey = new("Item.Key.Cellar");

    private readonly FlowRunTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void AppliesEffectsAlongTheLinksAndFinishes()
    {
        FlowRun run = _builder.Build("""
            {
              "nodes": [
                { "$type": "start", "id": "start" },
                { "$type": "applyEffects", "id": "open", "effects": { "effects": [ { "$type": "fact", "fact": "Quest.Cellar.Opened", "value": 2 } ] } },
                { "$type": "finish", "id": "end" }
              ],
              "links": [ { "from": "start", "to": "open" }, { "from": "open", "to": "end" } ]
            }
            """);
        int finished = 0;
        run.Finished += _ => finished++;

        run.Start();

        _builder.Facts.GetFact(Opened).Should().Be(2);
        run.IsRunning.Should().BeFalse();
        finished.Should().Be(1);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(0, 2)]
    public void BranchesOnItsConditions(int key, int expected)
    {
        _builder.Facts.SetFact(HasKey, key);
        FlowRun run = _builder.Build("""
            {
              "nodes": [
                { "$type": "start", "id": "start" },
                { "$type": "branch", "id": "key", "conditions": { "conditions": [ { "$type": "fact", "fact": "Item.Key.Cellar" } ] } },
                { "$type": "applyEffects", "id": "open", "effects": { "effects": [ { "$type": "fact", "fact": "Quest.Cellar.Opened", "value": 1 } ] } },
                { "$type": "applyEffects", "id": "locked", "effects": { "effects": [ { "$type": "fact", "fact": "Quest.Cellar.Opened", "value": 2 } ] } }
              ],
              "links": [
                { "from": "start", "to": "key" },
                { "from": "key", "output": "True", "to": "open" },
                { "from": "key", "output": "False", "to": "locked" }
              ]
            }
            """);

        run.Start();

        _builder.Facts.GetFact(Opened).Should().Be(expected);
    }

    [Fact]
    public void WaitsForConditionsUntilAFactChanges()
    {
        FlowRun run = _builder.Build("""
            {
              "nodes": [
                { "$type": "start", "id": "start" },
                { "$type": "waitForConditions", "id": "wait", "conditions": { "conditions": [ { "$type": "fact", "fact": "Item.Key.Cellar" } ] } },
                { "$type": "applyEffects", "id": "open", "effects": { "effects": [ { "$type": "fact", "fact": "Quest.Cellar.Opened" } ] } }
              ],
              "links": [ { "from": "start", "to": "wait" }, { "from": "wait", "to": "open" } ]
            }
            """);

        run.Start();
        bool waited = _builder.Facts.IsFactDefined(Opened);
        run.ActiveNodes.Should().ContainSingle().Which.Should().BeOfType<WaitForConditionsNode>();
        _builder.Facts.SetFact(HasKey, 1);

        waited.Should().BeFalse();
        _builder.Facts.GetFact(Opened).Should().Be(1);
        run.ActiveNodes.Should().BeEmpty();
        run.IsRunning.Should().BeTrue("no finish node was reached");
    }

    [Fact]
    public void StopsListeningToFactsWhenItFinishes()
    {
        FlowRun run = _builder.Build("""
            {
              "nodes": [
                { "$type": "start", "id": "start" },
                { "$type": "waitForConditions", "id": "wait", "conditions": { "conditions": [ { "$type": "fact", "fact": "Item.Key.Cellar" } ] } },
                { "$type": "applyEffects", "id": "open", "effects": { "effects": [ { "$type": "fact", "fact": "Quest.Cellar.Opened" } ] } }
              ],
              "links": [ { "from": "start", "to": "wait" }, { "from": "wait", "to": "open" } ]
            }
            """);
        run.Start();

        run.Finish();
        _builder.Facts.SetFact(HasKey, 1);

        run.ActiveNodes.Should().BeEmpty();
        _builder.Facts.IsFactDefined(Opened).Should().BeFalse();
    }

    [Fact]
    public void StopsALoopingGraph()
    {
        FlowRun run = _builder.Build("""
            {
              "nodes": [
                { "$type": "start", "id": "start" },
                { "$type": "applyEffects", "id": "count", "effects": { "effects": [ { "$type": "fact", "fact": "Quest.Cellar.Opened", "write": "Add" } ] } }
              ],
              "links": [ { "from": "start", "to": "count" }, { "from": "count", "to": "count" } ]
            }
            """);

        Action start = run.Start;

        start.Should().Throw<InvalidOperationException>().WithMessage("*loop*");
        run.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void ALinkToAMissingNodeThrows()
    {
        FlowRun run = _builder.Build("""{ "nodes": [ { "$type": "start", "id": "start" } ], "links": [ { "from": "start", "to": "gone" } ] }""");

        Action start = run.Start;

        start.Should().Throw<InvalidDataException>().WithMessage("*gone*");
    }
}
