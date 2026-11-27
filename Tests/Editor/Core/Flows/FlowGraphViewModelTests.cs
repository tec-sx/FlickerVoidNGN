using FlickerVoid.Editor.Core.Flows;
using FlickerVoid.Editor.Core.Menus;
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
    public void OffersEveryNodeTypeGroupedByItsModule()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);

        IReadOnlyList<MenuChoice> choices = canvas.GetAddChoices(10, 20);

        choices.Select(choice => choice.Name).Should().Contain(["Start", "Branch", "Apply Effects"]);
        choices.Should().OnlyContain(choice => choice.Group == "Flow");
    }

    [Fact]
    public void AddsTheChosenNodeWhereTheMenuOpened()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);

        canvas.GetAddChoices(10, 20).First(choice => choice.Name == "Branch").Run();

        FlowNodeViewModel added = canvas.Nodes[^1];
        added.Node.Should().BeOfType<BranchNode>();
        added.X.Should().Be(10);
        added.Y.Should().Be(20);
    }

    [Fact]
    public void WiresANodeAddedFromAnOutputToItsInput()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);
        FlowPinViewModel falseOutput = canvas.Nodes[1].Outputs[1];

        canvas.GetAddChoices(600, 100, falseOutput).First(choice => choice.Name == "Finish").Run();

        FlowLinkViewModel link = canvas.Links[^1];
        link.Output.Should().Be(falseOutput);
        link.Input.Node.Should().Be(canvas.Nodes[^1]);
    }

    [Fact]
    public void WiresANodeAddedFromAnInputToItsFirstOutput()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);
        FlowPinViewModel finishInput = canvas.Nodes[2].Inputs[0];

        canvas.GetAddChoices(300, 200, finishInput).First(choice => choice.Name == "Branch").Run();

        FlowLinkViewModel link = canvas.Links[^1];
        link.Input.Should().Be(finishInput);
        link.Output.Node.Should().Be(canvas.Nodes[^1]);
        link.Output.Name.Should().Be("True");
    }

    [Fact]
    public void OffersOnlyNodesThatCanBeWiredToTheDraggedPin()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);
        FlowPinViewModel startOutput = canvas.Nodes[0].Outputs[0];

        IReadOnlyList<MenuChoice> choices = canvas.GetAddChoices(0, 0, startOutput);

        choices.Select(choice => choice.Name).Should().NotContain("Start").And.Contain("Finish");
    }

    [Fact]
    public void SnapsADroppedWireToTheNearestFittingPinOfTheNodeUnderIt()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);
        FlowPinViewModel startOutput = canvas.Nodes[0].Outputs[0];
        FlowNodeViewModel branch = canvas.Nodes[1];

        FlowPinViewModel? target = canvas.FindDropTarget(branch.X + 150, branch.Y + 60, startOutput);

        target.Should().Be(branch.Inputs[0]);
    }

    [Fact]
    public void SnapsADroppedWireToAFittingPinNearTheNode()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);
        FlowPinViewModel branchTrue = canvas.Nodes[1].Outputs[0];
        FlowPinViewModel finishInput = canvas.Nodes[2].Inputs[0];

        FlowPinViewModel? near = canvas.FindDropTarget(finishInput.Point.X - 25, finishInput.Point.Y + 10, branchTrue);
        FlowPinViewModel? far = canvas.FindDropTarget(finishInput.Point.X - 80, finishInput.Point.Y, branchTrue);

        near.Should().Be(finishInput);
        far.Should().BeNull();
    }

    [Fact]
    public void NeverSnapsAWireToItsOwnNodeOrAPinOfTheSameSide()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);
        FlowPinViewModel branchTrue = canvas.Nodes[1].Outputs[0];
        FlowNodeViewModel branch = canvas.Nodes[1];
        FlowPinViewModel startOutput = canvas.Nodes[0].Outputs[0];

        FlowPinViewModel? own = canvas.FindDropTarget(branch.X + 90, branch.Y + 60, branchTrue);
        FlowPinViewModel? sameSide = canvas.FindDropTarget(startOutput.Point.X, startOutput.Point.Y, branchTrue);

        own.Should().BeNull();
        sameSide.Should().BeNull();
    }

    [Fact]
    public void FramesTheSelectedNodeOrEveryNode()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);
        FlowNodeViewModel finish = canvas.Nodes[2];

        canvas.FrameSelected(1000, 600);
        FlowPoint start = canvas.View.ToScreen(0, 0);
        canvas.Select(finish);
        canvas.FrameSelected(1000, 600);
        FlowPoint middle = canvas.View.ToScreen(finish.X + (FlowNodeViewModel.Width / 2), finish.Y + (finish.Height / 2));

        start.X.Should().BeGreaterThanOrEqualTo(0);
        middle.X.Should().BeApproximately(500, 1e-6);
        middle.Y.Should().BeApproximately(300, 1e-6);
    }

    [Fact]
    public void FindsThePinNearAPoint()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);
        FlowPinViewModel input = canvas.Nodes[1].Inputs[0];

        FlowPinViewModel? near = canvas.FindPin(input.Point.X - 9, input.Point.Y + 6);
        FlowPinViewModel? far = canvas.FindPin(input.Point.X - 40, input.Point.Y);

        near.Should().Be(input);
        far.Should().BeNull();
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

    [Fact]
    public void EachNodeHasAKindAndAnIconAndOnlyNodesWithTextHaveABody()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);

        canvas.Nodes.Select(node => node.Kind).Should().Equal("Start", "Condition", "Finish");
        canvas.Nodes.Select(node => node.Icon).Should().Equal("Play", "Branch", "Stop");
        canvas.Nodes[0].BodyHeight.Should().Be(0);
        canvas.Nodes[1].BodyHeight.Should().BeGreaterThan(0);
        canvas.Nodes[1].Inputs[0].Point.Y.Should().Be(canvas.Nodes[1].Y + FlowNodeViewModel.HeaderHeight + canvas.Nodes[1].BodyHeight + (FlowNodeViewModel.RowHeight / 2));
    }

    [Theory]
    [InlineData("line", "Dialogue", "Talk")]
    [InlineData("choice", "Choice", "Branch")]
    [InlineData("completeObjective", "Quest", "Flag")]
    [InlineData("switchShot", "Camera", "Camera")]
    [InlineData("applyEffects", "Effect", "Effects")]
    [InlineData("somethingNew", "Effect", "Effects")]
    public void GroupsNodeTypesByWhatTheyDo(string type, string kind, string icon)
    {
        FlowNodeLook look = FlowNodeLook.For(type);

        look.Kind.Should().Be(kind);
        look.Icon.Should().Be(icon);
    }

    [Fact]
    public void TheViewSaysItsZoomInPercent()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);

        canvas.View.ZoomAt(0, 0, 0);

        canvas.View.ZoomText.Should().Be("100%");
    }

    [Theory]
    [InlineData("Dialogue", "Innkeeper: Mind the floor.", "Innkeeper", "Mind the floor.")]
    [InlineData("Dialogue", "Owner: ", "Owner", "")]
    [InlineData("Quest", "Quest.Cellar: done", "", "Quest.Cellar: done")]
    [InlineData("Choice", "3 options", "", "3 options")]
    public void SplitsADialogueLineIntoItsSpeakerAndItsText(string kind, string description, string lead, string text)
    {
        (string Lead, string Text) parts = FlowNodeViewModel.SplitDescription(kind, description);

        parts.Lead.Should().Be(lead);
        parts.Text.Should().Be(text);
    }

    [Fact]
    public void RevealingANodeSelectsItAndAsksForItToBeFramed()
    {
        FlowGraphViewModel canvas = _builder.Build(Graph);
        int asked = 0;
        canvas.RevealRequested += () => asked++;

        canvas.Reveal(canvas.Nodes[2]);

        canvas.Selected.Should().Be(canvas.Nodes[2]);
        asked.Should().Be(1);
    }
}
