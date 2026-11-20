using FlickerVoid.Editor.Core.Flows;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Flow.Nodes;

namespace FlickerVoid.Editor.Core.Tests.Flows;

public sealed class FlowGraphViewModelTests : IDisposable
{
    private const string Graph = """
        {
          "nodes": [
            { "$type": "start", "id": "start", "x": 0, "y": 0 },
            { "$type": "branch", "id": "branch", "x": 240, "y": 20 },
            { "$type": "finish", "id": "finish", "x": 480, "y": 0 }
          ],
          "links": [ { "from": "start", "to": "branch" }, { "from": "branch", "output": "True", "to": "finish" } ]
        }
        """;

    private readonly FlowGraphViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShowsTheNodesAndWiresOfTheGraph()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);

        canvas.Nodes.Select(node => node.Title).Should().Equal("Start", "Branch", "Finish");
        canvas.Nodes[0].Inputs.Should().BeEmpty();
        canvas.Nodes[1].Outputs.Select(pin => pin.Name).Should().Equal("True", "False");
        canvas.Links.Should().HaveCount(2);
        canvas.Links[1].Output.Name.Should().Be("True");
        canvas.NodeChoices.Should().Contain(["start", "finish", "branch", "applyEffects", "waitForConditions"]);
    }

    [Fact]
    public void PlacesPinsOnTheSidesOfTheirNode()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);
        FlowNodeViewModel branch = canvas.Nodes[1];

        FlowPoint input = branch.Inputs[0].Point;
        FlowPoint falseOutput = branch.Outputs[1].Point;

        input.X.Should().Be(240);
        falseOutput.X.Should().Be(240 + FlowNodeViewModel.Width);
        falseOutput.Y.Should().BeGreaterThan(branch.Outputs[0].Point.Y);
    }

    [Fact]
    public void AddsANodeWithAFreeId()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);

        FlowNodeViewModel added = canvas.AddNode("branch", 100, 200);

        added.Id.Should().Be("branch2");
        added.Node.Should().BeOfType<BranchNode>();
        added.X.Should().Be(100);
        _builder.Graph.Nodes.Should().Contain(added.Node);
        canvas.Selected.Should().Be(added);
        _builder.Edits.Should().Be(1);
    }

    [Fact]
    public void WiresAnOutputToOneInputOnly()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);
        FlowNodeViewModel finish = canvas.Nodes[2];
        FlowNodeViewModel other = canvas.AddNode("finish", 480, 200);
        FlowPinViewModel trueOutput = canvas.Nodes[1].Outputs[0];

        bool connected = canvas.Connect(other.Inputs[0], trueOutput);

        connected.Should().BeTrue();
        _builder.Graph.Links.Should().HaveCount(2);
        FlowLink link = _builder.Graph.Links[1];
        link.From.Should().Be("branch");
        link.Output.Should().Be("True");
        link.To.Should().Be("finish2");
        canvas.Links.Should().NotContain(wire => wire.Input.Node == finish);
    }

    [Fact]
    public void RefusesToWireTwoInputsOrANodeToItself()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);
        FlowNodeViewModel branch = canvas.Nodes[1];

        bool inputs = canvas.Connect(branch.Inputs[0], canvas.Nodes[2].Inputs[0]);
        bool itself = canvas.Connect(branch.Outputs[1], branch.Inputs[0]);

        inputs.Should().BeFalse();
        itself.Should().BeFalse();
        _builder.Edits.Should().Be(0);
    }

    [Fact]
    public void RemovesANodeWithItsWires()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);
        FlowNodeViewModel branch = canvas.Nodes[1];
        canvas.Select(branch);

        canvas.RemoveSelected();

        canvas.Nodes.Should().HaveCount(2);
        canvas.Links.Should().BeEmpty();
        _builder.Graph.Links.Should().BeEmpty();
        canvas.Selected.Should().BeNull();
    }

    [Fact]
    public void DisconnectsAPin()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);

        canvas.Disconnect(canvas.Nodes[1].Inputs[0]);

        _builder.Graph.Links.Should().ContainSingle().Which.From.Should().Be("branch");
        _builder.Edits.Should().Be(1);
    }

    [Fact]
    public void MovingANodeMovesItsWires()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);
        FlowLinkViewModel wire = canvas.Links[0];
        List<string?> changed = [];
        wire.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        canvas.MoveNode(canvas.Nodes[1], 300, 50);

        _builder.Graph.Nodes[1].X.Should().Be(300);
        wire.End.X.Should().Be(300);
        changed.Should().Contain(nameof(FlowLinkViewModel.End));
    }

    [Fact]
    public void EditsTheSelectedNodesValuesButNotItsPlace()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);
        FlowNodeViewModel branch = canvas.Nodes[1];

        canvas.Select(branch);

        canvas.SelectedForm.Should().NotBeNull();
        canvas.SelectedForm!.Fields.Select(field => field.Label).Should().NotContain(["Id", "X", "Y"]);
        canvas.SelectedForm.Objects.Should().ContainSingle(section => section.Title == "Conditions");
        branch.IsSelected.Should().BeTrue();
    }
}
