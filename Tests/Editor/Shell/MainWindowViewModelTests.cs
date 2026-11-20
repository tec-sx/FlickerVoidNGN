using FlickerVoid.Editor.Assets;
using FlickerVoid.Engine.Rendering.Lighting;

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

    [Fact]
    public void ShowsTheDefaultMoodUntilAMoodFileIsOpened()
    {
        _builder.ViewModel.MoodHeading.Should().Be(MainWindowViewModel.DefaultMoodHeading);
    }

    [Fact]
    public void OpeningAMoodShowsItInTheViewportAndTheViewTab()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Night, AssetKind.Mood);

        _builder.ViewModel.Viewport.Mood.Exposure.Should().Be(0.5f);
        _builder.Exposure().Value.Should().Be(0.5);
        _builder.ViewModel.MoodHeading.Should().Be(MainWindowViewModelTestsBuilder.Night);
    }

    [Fact]
    public void HoldsTheViewTabsEditsOfAMoodUntilTheyAreSaved()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Night, AssetKind.Mood);

        _builder.Exposure().Value = 2;

        _builder.ViewModel.Viewport.Mood.Exposure.Should().Be(2);
        _builder.ViewModel.UnsavedPaths.Should().Equal(MainWindowViewModelTestsBuilder.Night);
        _builder.ViewModel.MoodHeading.Should().Be("Night.fvmood *");
        _builder.ViewModel.SaveAssets();
        _builder.ReadFile(MainWindowViewModelTestsBuilder.Night).Should().Contain("\"exposure\": 2");
        _builder.ViewModel.MoodHeading.Should().Be(MainWindowViewModelTestsBuilder.Night);
    }

    [Fact]
    public void ShowsTheSavedMoodAgainWhenItsEditsAreDiscarded()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Night, AssetKind.Mood);
        _builder.Exposure().Value = 2;

        _builder.ViewModel.DiscardAssets();

        _builder.ViewModel.Viewport.Mood.Exposure.Should().Be(0.5f);
        _builder.Exposure().Value.Should().Be(0.5);
    }

    [Fact]
    public void ShowsTheOpenMoodAgainWhenItChangesOnDisk()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Night, AssetKind.Mood);

        _builder.ChangeOnDisk(MainWindowViewModelTestsBuilder.Night, """{ "exposure": 4 }""");

        _builder.ViewModel.Viewport.Mood.Exposure.Should().Be(4);
        _builder.Exposure().Value.Should().Be(4);
    }

    [Fact]
    public void KeepsTheViewportsMoodWhenAMoodFileCannotBeRead()
    {
        _builder.WriteFile("Broken.fvmood", "{ not json");
        LightingMood shown = _builder.ViewModel.Viewport.Mood;

        _builder.OpenInBrowser("Broken.fvmood", AssetKind.Mood);

        _builder.ViewModel.Viewport.Mood.Should().BeSameAs(shown);
        _builder.ViewModel.MoodHeading.Should().Be(MainWindowViewModel.DefaultMoodHeading);
        _builder.ViewModel.Viewport.Status.Should().StartWith("Can't open Broken.fvmood");
    }
}
