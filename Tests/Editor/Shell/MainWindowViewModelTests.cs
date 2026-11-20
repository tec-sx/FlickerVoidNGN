namespace FlickerVoid.Editor.Tests;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly MainWindowViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void StarsTheTitleWhileTheSceneHasUnsavedEdits()
    {
        string saved = _builder.ViewModel.Title;
        _builder.EditScene();
        string edited = _builder.ViewModel.Title;
        _builder.ViewModel.Viewport.Save();

        saved.Should().Be("Room.fvscene - FlickerVoid Editor");
        edited.Should().Be("* Room.fvscene - FlickerVoid Editor");
        _builder.ViewModel.Title.Should().Be(saved);
    }

    [Fact]
    public void ListsTheSceneBeforeTheAssetsWithUnsavedEdits()
    {
        _builder.EditMaterial();
        _builder.EditScene();

        _builder.ViewModel.HasUnsavedChanges.Should().BeTrue();
        _builder.ViewModel.UnsavedPaths.Should().Equal(MainWindowViewModelTestsBuilder.Room, MainWindowViewModelTestsBuilder.Paper);
    }

    [Fact]
    public void SaveAllSavesTheSceneAndTheAssets()
    {
        _builder.EditScene();
        _builder.EditMaterial();

        _builder.ViewModel.SaveAll();

        _builder.ViewModel.HasUnsavedChanges.Should().BeFalse();
        _builder.ReadFile(MainWindowViewModelTestsBuilder.Room).Should().Contain("[4,0,0]");
        _builder.ReadFile(MainWindowViewModelTestsBuilder.Paper).Should().Contain("edited");
    }

    [Fact]
    public void OpensAnotherSceneWithoutAskingWhenNothingIsUnsaved()
    {
        _builder.EditMaterial();

        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Hall);

        _builder.Prompts.Should().BeEmpty();
        _builder.Scene.ScenePath.Should().Be(MainWindowViewModelTestsBuilder.Hall);
    }

    [Fact]
    public void SavesTheSceneBeforeOpeningAnotherWhenTheUserChoosesSave()
    {
        _builder.EditScene();
        _builder.Answer = UnsavedAssetsChoice.Save;

        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Hall);

        _builder.Prompts.Should().ContainSingle().Which.Should().Equal(MainWindowViewModelTestsBuilder.Room);
        _builder.ReadFile(MainWindowViewModelTestsBuilder.Room).Should().Contain("[4,0,0]");
        _builder.Scene.ScenePath.Should().Be(MainWindowViewModelTestsBuilder.Hall);
    }

    [Fact]
    public void DropsTheSceneEditsWhenTheUserChoosesDiscard()
    {
        _builder.EditScene();
        _builder.Answer = UnsavedAssetsChoice.Discard;

        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Hall);

        _builder.ReadFile(MainWindowViewModelTestsBuilder.Room).Should().NotContain("[4,0,0]");
        _builder.Scene.ScenePath.Should().Be(MainWindowViewModelTestsBuilder.Hall);
        _builder.Scene.IsUnsaved.Should().BeFalse();
    }

    [Fact]
    public void KeepsTheSceneOpenWhenTheUserCancels()
    {
        _builder.EditScene();
        _builder.Answer = null;

        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Hall);

        _builder.Scene.ScenePath.Should().Be(MainWindowViewModelTestsBuilder.Room);
        _builder.Scene.IsUnsaved.Should().BeTrue();
    }
}
