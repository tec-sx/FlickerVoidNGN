using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Editor.Tests;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly MainWindowViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void CompilesOnlyWithAProjectOpen()
    {
        _builder.ViewModel.CompileCommand.CanExecute(null).Should().BeFalse("only a project has code to compile");
    }

    [Fact]
    public void OpensVisualStudioOnlyWithAProjectSolution()
    {
        _builder.ViewModel.OpenInVisualStudioCommand.CanExecute(null).Should().BeFalse("only a project has a solution to open");
    }

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
    public void OpeningADataAssetEditsItInTheDataTabUntilItIsSaved()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Lantern, AssetTypes.Data);

        _builder.ViewModel.SideTab.Should().Be(MainWindowViewModel.DataTab);
        NumberFieldViewModel hours = (NumberFieldViewModel)((Core.Data.DataObjectViewModel)_builder.ViewModel.DataContent!).Fields.Single();
        hours.Value.Should().Be(6);
        hours.Value = 9;
        _builder.ViewModel.UnsavedPaths.Should().Equal(MainWindowViewModelTestsBuilder.Lantern);
        _builder.ViewModel.DataHeading.Should().Be("Lantern.fvdata *");
        _builder.ViewModel.SaveAssets();
        _builder.ReadFile(MainWindowViewModelTestsBuilder.Lantern).Should().Contain("\"hours\": 9").And.Contain("\"type\": \"lantern\"");
    }

    [Fact]
    public void UndoesADataAssetEditAndShowsTheFormAgain()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Lantern, AssetTypes.Data);
        NumberFieldViewModel hours = (NumberFieldViewModel)((Core.Data.DataObjectViewModel)_builder.ViewModel.DataContent!).Fields.Single();
        hours.Value = 9;

        _builder.ViewModel.SidePanelFocused = true;
        _builder.ViewModel.GetHistory()!.Undo();

        NumberFieldViewModel shown = (NumberFieldViewModel)((Core.Data.DataObjectViewModel)_builder.ViewModel.DataContent!).Fields.Single();
        shown.Value.Should().Be(6);
        _builder.ViewModel.UnsavedPaths.Should().BeEmpty();
    }

    [Fact]
    public void UndoesAFlowGraphEditInItsEditor()
    {
        Core.Flows.FlowGraphViewModel? requested = null;
        _builder.ViewModel.FlowRequested += (_, graph) => requested = graph;
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Cellar, AssetTypes.Flow);
        requested!.AddNode("finish", 200, 0);

        _builder.ViewModel.GetFlowHistory(MainWindowViewModelTestsBuilder.Cellar).Undo();

        requested.Nodes.Should().ContainSingle();
    }

    [Fact]
    public void UndoesSceneEditsWhenTheKeyboardIsOutsideTheSidePanel()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Lantern, AssetTypes.Data);

        UndoHistory? history = _builder.ViewModel.GetHistory();

        history.Should().BeSameAs(_builder.Scene.History);
    }

    [Fact]
    public void UndoesAndRedoesTheSceneFromTheTopBar()
    {
        _builder.ViewModel.UndoCommand.CanExecute(null).Should().BeFalse();
        _builder.EditScene();

        _builder.ViewModel.UndoCommand.CanExecute(null).Should().BeTrue();
        _builder.ViewModel.UndoCommand.Execute(null);

        _builder.Scene.History.CanRedo.Should().BeTrue();
        _builder.ViewModel.UndoCommand.CanExecute(null).Should().BeFalse();
        _builder.ViewModel.RedoCommand.CanExecute(null).Should().BeTrue();
        _builder.ViewModel.RedoCommand.Execute(null);
        _builder.Scene.History.CanUndo.Should().BeTrue();
    }

    [Fact]
    public void TheTopBarUndoesWhatTheKeyboardIsIn()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Lantern, AssetTypes.Data);
        NumberFieldViewModel hours = (NumberFieldViewModel)((Core.Data.DataObjectViewModel)_builder.ViewModel.DataContent!).Fields.Single();
        hours.Value = 9;
        _builder.ViewModel.UndoCommand.CanExecute(null).Should().BeFalse();

        _builder.ViewModel.SidePanelFocused = true;

        _builder.ViewModel.UndoTip.Should().Contain("Ctrl+Z");
        _builder.ViewModel.UndoCommand.CanExecute(null).Should().BeTrue();
        _builder.ViewModel.UndoCommand.Execute(null);
        _builder.ViewModel.UnsavedPaths.Should().BeEmpty();
    }

    [Fact]
    public void OpensFlowGraphsInAGraphEditorWithTheirEditsHeldUnsaved()
    {
        Core.Flows.FlowGraphViewModel? requested = null;
        _builder.ViewModel.FlowRequested += (_, graph) => requested = graph;

        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Cellar, AssetTypes.Flow);

        requested.Should().NotBeNull();
        requested!.Nodes.Should().ContainSingle();
        requested.AddNode("finish", 200, 0);
        _builder.ViewModel.UnsavedPaths.Should().Equal(MainWindowViewModelTestsBuilder.Cellar);
        _builder.ViewModel.SaveAssets();
        _builder.ReadFile(MainWindowViewModelTestsBuilder.Cellar).Should().Contain("\"$type\": \"finish\"");
    }

    [Fact]
    public void ShowsTheDefaultMoodUntilAMoodFileIsOpened()
    {
        _builder.ViewModel.MoodHeading.Should().Be(MainWindowViewModel.DefaultMoodHeading);
    }

    [Fact]
    public void OpeningAMoodShowsItInTheViewportAndTheViewTab()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Night, AssetTypes.Mood);

        _builder.ViewModel.Viewport.Mood.Exposure.Should().Be(0.5f);
        _builder.Exposure().Value.Should().Be(0.5);
        _builder.ViewModel.MoodHeading.Should().Be(MainWindowViewModelTestsBuilder.Night);
    }

    [Fact]
    public void HoldsTheViewTabsEditsOfAMoodUntilTheyAreSaved()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Night, AssetTypes.Mood);

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
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Night, AssetTypes.Mood);
        _builder.Exposure().Value = 2;

        _builder.ViewModel.DiscardAssets();

        _builder.ViewModel.Viewport.Mood.Exposure.Should().Be(0.5f);
        _builder.Exposure().Value.Should().Be(0.5);
    }

    [Fact]
    public void ShowsTheOpenMoodAgainWhenItChangesOnDisk()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Night, AssetTypes.Mood);

        _builder.ChangeOnDisk(MainWindowViewModelTestsBuilder.Night, """{ "exposure": 4 }""");

        _builder.ViewModel.Viewport.Mood.Exposure.Should().Be(4);
        _builder.Exposure().Value.Should().Be(4);
    }

    [Fact]
    public void KeepsTheViewportsMoodWhenAMoodFileCannotBeRead()
    {
        _builder.WriteFile("Broken.fvmood", "{ not json");
        LightingMood shown = _builder.ViewModel.Viewport.Mood;

        _builder.OpenInBrowser("Broken.fvmood", AssetTypes.Mood);

        _builder.ViewModel.Viewport.Mood.Should().BeSameAs(shown);
        _builder.ViewModel.MoodHeading.Should().Be(MainWindowViewModel.DefaultMoodHeading);
        _builder.ViewModel.Viewport.Status.Should().StartWith("Can't open Broken.fvmood");
    }

    [Fact]
    public void LightsTheViewportWithTheMoodAnOpenedSceneNames()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Lit);

        _builder.ViewModel.Viewport.Mood.Exposure.Should().Be(0.5f);
        _builder.ViewModel.MoodHeading.Should().Be(MainWindowViewModelTestsBuilder.Night);
    }

    [Fact]
    public void ShowsTheMoodTheSceneIsGiven()
    {
        _builder.NameSceneMood(MainWindowViewModelTestsBuilder.Night);

        _builder.ViewModel.Viewport.Mood.Exposure.Should().Be(0.5f);
        _builder.ViewModel.MoodHeading.Should().Be(MainWindowViewModelTestsBuilder.Night);
    }

    [Fact]
    public void GoesBackToTheDefaultMoodForASceneThatNamesNone()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Lit);

        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Hall);

        _builder.ViewModel.MoodHeading.Should().Be(MainWindowViewModel.DefaultMoodHeading);
        _builder.ViewModel.Viewport.Mood.Should().Be(new LightingMood());
    }
}
