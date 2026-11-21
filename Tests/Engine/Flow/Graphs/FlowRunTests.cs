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
    public void RunsALongChainWithoutDeepCalls()
    {
        const int Length = 5_000;
        System.Text.StringBuilder nodes = new("""{ "$type": "start", "id": "n0" }""");
        System.Text.StringBuilder links = new();
        for (int index = 1; index <= Length; index++)
        {
            nodes.Append($$""", { "$type": "applyEffects", "id": "n{{index}}", "effects": { "effects": [ { "$type": "fact", "fact": "Quest.Cellar.Opened", "write": "Add" } ] } }""");
            if (index > 1)
            {
                links.Append(", ");
            }

            links.Append($$"""{ "from": "n{{index - 1}}", "to": "n{{index}}" }""");
        }

        FlowRun run = _builder.Build($$"""{ "nodes": [ {{nodes}} ], "links": [ {{links}} ] }""");
        Thread thread = new(run.Start, maxStackSize: 256 * 1024);

        thread.Start();
        thread.Join();

        _builder.Facts.GetFact(Opened).Should().Be(Length);
    }

    [Fact]
    public void PassesSignalsDepthFirstInTheLinksOrder()
    {
        FlowRun run = _builder.Build("""
            {
              "nodes": [
                { "$type": "start", "id": "start" },
                { "$type": "applyEffects", "id": "a", "effects": { "effects": [ { "$type": "fact", "fact": "Quest.Cellar.Opened", "value": 1 } ] } },
                { "$type": "applyEffects", "id": "b", "effects": { "effects": [ { "$type": "fact", "fact": "Quest.Cellar.Opened", "value": 2 } ] } },
                { "$type": "applyEffects", "id": "c", "effects": { "effects": [ { "$type": "fact", "fact": "Quest.Cellar.Opened", "value": 3 } ] } }
              ],
              "links": [ { "from": "start", "to": "a" }, { "from": "start", "to": "c" }, { "from": "a", "to": "b" } ]
            }
            """);

        run.Start();

        _builder.Facts.GetFact(Opened).Should().Be(3, "a and the b after it run before c");
    }

    [Fact]
    public void ALinkToAMissingNodeThrows()
    {
        Action build = () => _builder.Build("""{ "nodes": [ { "$type": "start", "id": "start" } ], "links": [ { "from": "start", "to": "gone" } ] }""");

        build.Should().Throw<InvalidDataException>().WithMessage("*gone*");
    }

    [Fact]
    public void ALinkFromAMissingPinThrows()
    {
        Action build = () => _builder.Build("""
            {
              "nodes": [ { "$type": "branch", "id": "branch" }, { "$type": "finish", "id": "end" } ],
              "links": [ { "from": "branch", "output": "Maybe", "to": "end" } ]
            }
            """);

        build.Should().Throw<InvalidDataException>().WithMessage("*branch*output Maybe*");
    }

    [Fact]
    public void NamesPinsAfterTheNodesEnums()
    {
        BranchNode branch = new();
        StartNode start = new();
        FinishNode finish = new();

        branch.Inputs.Should().Equal("In");
        branch.Outputs.Should().Equal("True", "False");
        start.Inputs.Should().BeEmpty();
        finish.Outputs.Should().BeEmpty();
    }
}
