using System.Text;

using FlickerVoid.Editor.Core.Undo;

namespace FlickerVoid.Editor.Assets.Tests;

public sealed class UnsavedContentUndoTests : IDisposable
{
    private const string Saved = """{ "parameters": { "specular": 0.5 } }""";
    private const string First = """{ "parameters": { "specular": 1 } }""";
    private const string Second = """{ "parameters": { "specular": 2 } }""";

    private readonly UnsavedContentTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void UndoesAnEditBackToTheFileOnDiskAndRedoesIt()
    {
        UnsavedContent content = _builder.WithFile("Brick.fvmat", Saved).Build();
        List<string> restored = [];
        content.Restored += restored.Add;
        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(First));
        UndoHistory history = content.GetHistory("Brick.fvmat");

        history.Undo();

        content.IsUnsaved("Brick.fvmat").Should().BeFalse();
        Read(content).Should().Be(Saved);
        restored.Should().ContainSingle();
        history.Redo();
        content.IsUnsaved("Brick.fvmat").Should().BeTrue();
        Read(content).Should().Be(First);
    }

    [Fact]
    public void UndoesEditsOneAtATime()
    {
        UnsavedContent content = _builder.WithFile("Brick.fvmat", Saved).Build();
        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(First));
        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(Second));

        content.GetHistory("Brick.fvmat").Undo();

        Read(content).Should().Be(First);
        content.IsUnsaved("Brick.fvmat").Should().BeTrue();
    }

    [Fact]
    public void KeepsTheHistoryThroughASaveAndUndoesPastIt()
    {
        UnsavedContent content = _builder.WithFile("Brick.fvmat", Saved).Build();
        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(First));
        content.Save("Brick.fvmat");

        content.GetHistory("Brick.fvmat").Undo();

        content.IsUnsaved("Brick.fvmat").Should().BeTrue();
        Read(content).Should().Be(Saved);
    }

    [Fact]
    public void ForgetsTheHistoryWhenTheEditsAreDiscarded()
    {
        UnsavedContent content = _builder.WithFile("Brick.fvmat", Saved).Build();
        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(First));

        content.Discard("Brick.fvmat");

        content.GetHistory("Brick.fvmat").CanUndo.Should().BeFalse();
    }

    [Fact]
    public void ForgetsTheHistoryWhenTheFileChangesOnDisk()
    {
        UnsavedContent content = _builder.WithFile("Brick.fvmat", Saved).Build();
        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(First));
        content.Save("Brick.fvmat");

        _builder.ChangeOnDisk("Brick.fvmat", Second);

        content.GetHistory("Brick.fvmat").CanUndo.Should().BeFalse();
    }

    private static string Read(UnsavedContent content)
    {
        byte[] bytes = content.Read("Brick.fvmat");
        return Encoding.UTF8.GetString(bytes);
    }
}
