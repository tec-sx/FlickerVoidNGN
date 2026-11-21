using FlickerVoid.Editor.Assets.Flows;
using FlickerVoid.Engine.Flow.Nodes;

namespace FlickerVoid.Editor.Assets.Tests.Flows;

public sealed class FlowDocumentTests : IDisposable
{
    private readonly FlowDocumentTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ReadsTheGraph()
    {
        FlowDocument document = _builder.Build();

        document.Graph.Nodes.Should().HaveCount(2);
        document.Graph.Nodes[1].Should().BeOfType<FinishNode>().Which.X.Should().Be(200);
    }

    [Fact]
    public void HoldsEditsUnsavedUntilTheyAreSaved()
    {
        FlowDocument document = _builder.Build();

        document.Graph.Nodes[1].X = 320;
        document.Hold();

        _builder.Content.IsUnsaved(FlowDocumentTestsBuilder.Cellar).Should().BeTrue();
        _builder.ReadFile().Should().Contain("200");
        _builder.Content.Save(FlowDocumentTestsBuilder.Cellar);
        string saved = _builder.ReadFile();
        saved.Should().Contain("\"x\": 320").And.Contain("\"$type\": \"finish\"").And.Contain("\"$schema\": \"../.schemas/flow.schema.json\"");
    }

    [Fact]
    public void OpensWithTheUnsavedEdits()
    {
        FlowDocument first = _builder.Build();
        first.Graph.Nodes.RemoveAt(1);
        first.Hold();

        FlowDocument again = _builder.Build();

        again.Graph.Nodes.Should().ContainSingle();
    }
}
