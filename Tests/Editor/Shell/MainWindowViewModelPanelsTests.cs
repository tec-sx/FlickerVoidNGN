using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Documents;
using FlickerVoid.Editor.Core.Menus;
using FlickerVoid.Editor.Persistence.Layout;
using FlickerVoid.Editor.Viewports;

namespace FlickerVoid.Editor.Tests;

/// <summary>
/// The side panels: the context panel shows only what is selected or open, newest in front, and the outliner holds the scene, the
/// selection's tree, the view and, in the creator, the parts.
/// </summary>
public sealed class MainWindowViewModelPanelsTests : IDisposable
{
    private readonly MainWindowViewModelTestsBuilder _builder = new();

    private MainWindowViewModel ViewModel => _builder.ViewModel;

    private ViewportViewModel Viewport => _builder.ViewModel.Viewport;

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TheCommandPaletteListsCommandsWithTheirKeysAndRunsThem()
    {
        IReadOnlyList<MenuChoice> choices = ViewModel.PaletteChoices;
        MenuChoice content = choices.Single(choice => choice.Name == "Open or close the content drawer");

        content.Run();

        content.Gesture.Should().Be("Ctrl+Space");
        ViewModel.IsContentOpen.Should().BeTrue();
        choices.Select(choice => choice.Name).Should().Contain(["Play", "Compile", "Save assets", "Undo", "Redo", "Swap the outliner and the context panel"]);
        choices.Should().OnlyContain(choice => choice.Group != null);
    }

    [Fact]
    public void SaveSavesTheDocumentShownInPlaceOfTheViewport()
    {
        int saved = 0;
        RelayCommand save = new(() => saved++);
        EditorDocument texture = new("Textures/Brick.png", "Brick.png", new object(), () => { }, save);
        ViewModel.Documents.Show(texture);

        ViewModel.SaveShownCommand.Execute(null);

        saved.Should().Be(1);
    }

    [Fact]
    public void TheContentDrawerIsClosedUntilItIsToggled()
    {
        bool closed = ViewModel.IsContentOpen;
        double closedHeight = ViewModel.ContentRowHeight.Value;

        ViewModel.ToggleContentCommand.Execute(null);
        bool opened = ViewModel.IsContentOpen;
        double openHeight = ViewModel.ContentRowHeight.Value;
        ViewModel.ToggleContentCommand.Execute(null);

        closed.Should().BeFalse();
        closedHeight.Should().Be(0);
        opened.Should().BeTrue();
        openHeight.Should().BeGreaterThan(0);
        ViewModel.IsContentOpen.Should().BeFalse();
    }

    [Fact]
    public void TheContextPanelIsEmptyWithNothingSelectedOrOpen()
    {
        ViewModel.HasContext.Should().BeFalse();
        ViewModel.ShowsInspector.Should().BeFalse();
        ViewModel.ShowsData.Should().BeFalse();
        ViewModel.ShowsCreator.Should().BeFalse();
        ViewModel.SideTab.Should().Be(MainWindowViewModel.NoTab);
    }

    [Fact]
    public void SelectingAnEntityBringsTheInspectorAndDeselectingTakesItAway()
    {
        Viewport.Selection.Select(_builder.Find("Lamp"));
        int selected = ViewModel.SideTab;
        bool shown = ViewModel.ShowsInspector;

        Viewport.Selection.Select(null);

        selected.Should().Be(MainWindowViewModel.InspectorTab);
        shown.Should().BeTrue();
        ViewModel.ShowsInspector.Should().BeFalse();
        ViewModel.SideTab.Should().Be(MainWindowViewModel.NoTab);
    }

    [Fact]
    public void OpeningADataAssetComesInFrontAndClosingItGoesBackToTheInspector()
    {
        Viewport.Selection.Select(_builder.Find("Lamp"));

        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Lantern, AssetTypes.Data);
        int opened = ViewModel.SideTab;
        ViewModel.CloseData();

        opened.Should().Be(MainWindowViewModel.DataTab);
        ViewModel.ShowsData.Should().BeFalse();
        ViewModel.DataContent.Should().BeNull();
        ViewModel.ShowsInspector.Should().BeTrue();
        ViewModel.SideTab.Should().Be(MainWindowViewModel.InspectorTab, "the tab shown before comes back");
    }

    [Fact]
    public void SelectingAnEntityWhileADataAssetIsOpenBringsTheInspectorInFront()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Lantern, AssetTypes.Data);

        Viewport.Selection.Select(_builder.Find("Lamp"));

        ViewModel.SideTab.Should().Be(MainWindowViewModel.InspectorTab);
        ViewModel.ShowsData.Should().BeTrue("the data asset stays open behind it");
    }

    [Fact]
    public void OpeningAMoodShowsItInTheOutlinersViewTab()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Night, AssetTypes.Mood);

        ViewModel.OutlinerTab.Should().Be(MainWindowViewModel.ViewTab);
    }

    [Fact]
    public void UndoesTheMoodWhenTheKeyboardIsInTheViewTab()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Night, AssetTypes.Mood);
        _builder.Exposure().Value = 2;

        ViewModel.OutlinerFocused = true;
        ViewModel.Undo();

        _builder.Exposure().Value.Should().Be(0.5);
    }

    [Fact]
    public void SwappingPutsTheOutlinerOverTheContextPanelAndIsRemembered()
    {
        int contextRow = ViewModel.ContextRow;
        int outlinerRow = ViewModel.OutlinerRow;

        ViewModel.SwapPanels();

        contextRow.Should().BeLessThan(outlinerRow, "the context panel starts on top");
        ViewModel.ContextRow.Should().Be(outlinerRow);
        ViewModel.OutlinerRow.Should().Be(contextRow);
        new EditorLayout(_builder.LayoutFile).PanelsSwapped.Should().BeTrue();
    }
}
