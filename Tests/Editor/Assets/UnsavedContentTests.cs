using System.Text;

using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Editor.Assets.Tests;

public sealed class UnsavedContentTests : IDisposable
{
    private const string Saved = """{ "parameters": { "specular": 0.5 } }""";
    private const string Edited = """{ "parameters": { "specular": 2 } }""";

    private readonly UnsavedContentTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void HoldsAnEditWithoutWritingTheFile()
    {
        _builder.WithFile("Brick.fvmat", Saved);
        UnsavedContent content = _builder.Build();

        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(Edited));

        _builder.ReadFile("Brick.fvmat").Should().Be(Saved);
        content.IsUnsaved("Brick.fvmat").Should().BeTrue();
        content.HasUnsaved.Should().BeTrue();
        content.UnsavedPaths.Should().Equal("Brick.fvmat");
    }

    [Fact]
    public void LoadsTheEditedContents()
    {
        _builder.WithFile("Brick.fvmat", Saved);
        UnsavedContent content = _builder.Build();
        content.Load("Brick.fvmat", RenderingJsonContext.Default.MaterialFile);

        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(Edited));

        MaterialFile loaded = content.Load("Brick.fvmat", RenderingJsonContext.Default.MaterialFile);
        loaded.Parameters["specular"].GetSingle().Should().Be(2);
    }

    [Fact]
    public void ReportsAnEditAsAChange()
    {
        _builder.WithFile("Brick.fvmat", Saved);
        UnsavedContent content = _builder.Build();
        List<string> changed = [];
        int unsavedChanges = 0;
        content.Changed += changed.Add;
        content.UnsavedChanged += () => unsavedChanges++;

        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(Edited));
        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(Edited));

        changed.Should().HaveCount(2).And.AllSatisfy(path => path.Should().EndWith("Brick.fvmat"));
        unsavedChanges.Should().Be(1);
    }

    [Fact]
    public void RefusesToHoldOtherFilesUnsaved()
    {
        _builder.WithFile("Level.fvscene", "{}");
        UnsavedContent content = _builder.Build();

        Action edit = () => content.Edit("Level.fvscene", Encoding.UTF8.GetBytes("{}"));

        edit.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SavesTheEditToTheFile()
    {
        _builder.WithFile("Brick.fvmat", Saved);
        UnsavedContent content = _builder.Build();
        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(Edited));

        content.Save("Brick.fvmat");

        _builder.ReadFile("Brick.fvmat").Should().Be(Edited);
        content.HasUnsaved.Should().BeFalse();
    }

    [Fact]
    public void DoesNotReportItsOwnSaveAgainWhenTheWatcherSeesIt()
    {
        _builder.WithFile("Brick.fvmat", Saved);
        UnsavedContent content = _builder.Build();
        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(Edited));
        content.Save("Brick.fvmat");
        List<string> changed = [];
        content.Changed += changed.Add;

        _builder.Disk.NotifyChanged("Brick.fvmat");

        changed.Should().BeEmpty();
    }

    [Fact]
    public void ReportsAnotherProgramsChangeAfterItsOwnSave()
    {
        _builder.WithFile("Brick.fvmat", Saved);
        UnsavedContent content = _builder.Build();
        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(Edited));
        content.Save("Brick.fvmat");
        List<string> changed = [];
        content.Changed += changed.Add;

        _builder.ChangeOnDisk("Brick.fvmat", Saved);

        changed.Should().ContainSingle().Which.Should().EndWith("Brick.fvmat");
    }

    [Fact]
    public void DiscardsTheEditAndLoadsTheFileAgain()
    {
        _builder.WithFile("Brick.fvmat", Saved);
        UnsavedContent content = _builder.Build();
        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(Edited));
        List<string> changed = [];
        content.Changed += changed.Add;

        content.Discard("Brick.fvmat");

        content.HasUnsaved.Should().BeFalse();
        changed.Should().ContainSingle().Which.Should().EndWith("Brick.fvmat");
        MaterialFile loaded = content.Load("Brick.fvmat", RenderingJsonContext.Default.MaterialFile);
        loaded.Parameters["specular"].GetSingle().Should().Be(0.5f);
    }

    [Fact]
    public void KeepsTheEditWhenTheFileChangesOnDisk()
    {
        _builder.WithFile("Brick.fvmat", Saved);
        UnsavedContent content = _builder.Build();
        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(Edited));
        List<string> changed = [];
        List<string> changedOnDisk = [];
        content.Changed += changed.Add;
        content.ChangedOnDisk += changedOnDisk.Add;

        _builder.ChangeOnDisk("Brick.fvmat", """{ "parameters": { "specular": 1 } }""");

        changed.Should().BeEmpty();
        changedOnDisk.Should().ContainSingle().Which.Should().EndWith("Brick.fvmat");
        string held = Encoding.UTF8.GetString(content.Read("Brick.fvmat"));
        held.Should().Be(Edited);
    }

    [Fact]
    public void ReportsChangesToFilesWithoutEdits()
    {
        _builder.WithFile("Brick.fvmat", Saved);
        UnsavedContent content = _builder.Build();
        List<string> changed = [];
        content.Changed += changed.Add;

        _builder.ChangeOnDisk("Brick.fvmat", Edited);

        changed.Should().ContainSingle().Which.Should().EndWith("Brick.fvmat");
    }

    [Fact]
    public void ForgetsTheEditsOfADeletedFolder()
    {
        _builder.WithFile("Materials/Brick.fvmat", Saved);
        UnsavedContent content = _builder.Build();
        content.Edit("Materials/Brick.fvmat", Encoding.UTF8.GetBytes(Edited));

        content.Delete("Materials");

        content.HasUnsaved.Should().BeFalse();
    }

    [Fact]
    public void SavesEveryEdit()
    {
        _builder.WithFile("Brick.fvmat", Saved).WithFile("Night.fvmood", "{}");
        UnsavedContent content = _builder.Build();
        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(Edited));
        content.Edit("Night.fvmood", Encoding.UTF8.GetBytes("""{ "exposure": 1 }"""));

        content.SaveAll();

        content.HasUnsaved.Should().BeFalse();
        _builder.ReadFile("Brick.fvmat").Should().Be(Edited);
        _builder.ReadFile("Night.fvmood").Should().Be("""{ "exposure": 1 }""");
    }

    [Fact]
    public void DiscardsEveryEdit()
    {
        _builder.WithFile("Brick.fvmat", Saved).WithFile("Night.fvmood", "{}");
        UnsavedContent content = _builder.Build();
        content.Edit("Brick.fvmat", Encoding.UTF8.GetBytes(Edited));
        content.Edit("Night.fvmood", Encoding.UTF8.GetBytes("""{ "exposure": 1 }"""));

        content.DiscardAll();

        content.HasUnsaved.Should().BeFalse();
        _builder.ReadFile("Brick.fvmat").Should().Be(Saved);
        _builder.ReadFile("Night.fvmood").Should().Be("{}");
    }
}
