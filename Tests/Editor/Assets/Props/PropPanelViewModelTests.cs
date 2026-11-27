using FlickerVoid.Editor.Assets.Props;

namespace FlickerVoid.Editor.Assets.Tests.Props;

public sealed class PropPanelViewModelTests : IDisposable
{
    private readonly PropPanelViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void AddsABoxOfTheSizeBesideTheOthersAndChoosesIt()
    {
        var panel = _builder.Build();
        panel.Width = 2;
        panel.Height = 0.5;

        panel.AddBox();

        panel.Boxes.Should().Equal("part.1", "part.2");
        panel.Chosen.Should().Be("part.2");
        var added = _builder.Load().Parts[1].Part;
        added.Place.Position.X.Should().Be(0.5f + PropDocument.Gap + 1, "it stands a gap to the right of the first box");
        added.Mesh.Points.Max(point => point.Y).Should().Be(0.5f);
    }

    [Fact]
    public void RemovesTheChosenPartButNeverTheLast()
    {
        var panel = _builder.Build();
        panel.RemoveCommand.CanExecute(null).Should().BeFalse();
        panel.AddBox();
        panel.Chosen = "part.1";

        panel.Remove();

        panel.Boxes.Should().Equal("part.2");
        _builder.Load().Parts.Should().ContainSingle();
        panel.RemoveCommand.CanExecute(null).Should().BeFalse();
    }
}
