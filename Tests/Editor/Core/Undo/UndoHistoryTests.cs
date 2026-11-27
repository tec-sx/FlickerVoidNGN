namespace FlickerVoid.Editor.Core.Tests.Undo;

public sealed class UndoHistoryTests
{
    private readonly UndoHistoryTestsBuilder _builder = new();

    [Fact]
    public void UndoesAndRedoesTheLastEdit()
    {
        _builder.Set(1);
        _builder.Set(2, "Move Lamp");

        _builder.History.UndoName.Should().Be("Move Lamp");
        _builder.History.Undo().Should().BeTrue();
        _builder.Value.Should().Be(1);
        _builder.History.RedoName.Should().Be("Move Lamp");

        _builder.History.Redo().Should().BeTrue();
        _builder.Value.Should().Be(2);
        _builder.History.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void RecordsNothingWhileUndoing()
    {
        _builder.Set(1);

        _builder.History.Undo();

        _builder.History.CanUndo.Should().BeFalse();
        _builder.History.CanRedo.Should().BeTrue();
    }

    [Fact]
    public void ANewEditClearsWhatCouldBeRedone()
    {
        _builder.Set(1);
        _builder.History.Undo();

        _builder.Set(5);

        _builder.History.CanRedo.Should().BeFalse();
        _builder.History.Redo().Should().BeFalse();
    }

    [Fact]
    public void RecordsAGroupAsOneStepKeepingItsFirstBeforeAndLastAfter()
    {
        _builder.Set(1);
        _builder.History.BeginGroup("Drag");
        _builder.Set(2);
        _builder.Set(3);
        _builder.Set(4);
        _builder.History.EndGroup();

        _builder.History.Undo();
        _builder.Value.Should().Be(1);
        _builder.History.Redo();
        _builder.Value.Should().Be(4);
        _builder.History.Undo();
        _builder.History.Undo();
        _builder.Value.Should().Be(0);
    }

    [Fact]
    public void DropsTheOldestStepsPastTheLimits()
    {
        _builder.WithLimits(maxSteps: 2, maxBytes: 100);
        _builder.Set(1, size: 10);
        _builder.Set(2, size: 10);
        _builder.Set(3, size: 10);
        _builder.Set(4, size: 90);

        _builder.History.Undo();
        _builder.History.Undo();
        _builder.Value.Should().Be(2);
        _builder.History.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void CannotUndoInTheMiddleOfAGroup()
    {
        _builder.Set(1);
        _builder.History.BeginGroup("Drag");
        _builder.Set(2);

        _builder.History.Undo().Should().BeFalse();
        _builder.History.EndGroup();
        _builder.History.Undo().Should().BeTrue();
        _builder.Value.Should().Be(1);
    }

    [Fact]
    public void ACancelledGroupPutsEverythingBackAndRecordsNothing()
    {
        _builder.Set(1);
        _builder.History.BeginGroup("Drag");
        _builder.Set(2);
        _builder.Set(3);

        _builder.History.CancelGroup();

        _builder.Value.Should().Be(1);
        _builder.History.CanRedo.Should().BeFalse();
        _builder.History.Undo();
        _builder.Value.Should().Be(0);
    }
}
