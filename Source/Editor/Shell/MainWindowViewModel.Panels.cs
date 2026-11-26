using Avalonia.Controls;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Menus;
using FlickerVoid.Editor.Persistence.Layout;

namespace FlickerVoid.Editor;

public sealed partial class MainWindowViewModel
{
    /// <summary>The context panel's index when it shows nothing.</summary>
    public const int NoTab = -1;

    /// <summary>The context panel's Inspector tab, shown while an entity is selected.</summary>
    public const int InspectorTab = 0;

    /// <summary>The context panel's Data tab, shown while a data asset or table is open.</summary>
    public const int DataTab = 1;

    /// <summary>The context panel's Vehicle tab, shown while a vehicle is made.</summary>
    public const int CreatorTab = 2;

    /// <summary>The outliner's Scene tab, hidden in the creator.</summary>
    public const int SceneTab = 0;

    /// <summary>The outliner's Entity tab, the selection's whole tree, hidden in the creator.</summary>
    public const int EntityTab = 1;

    /// <summary>The outliner's View tab: the mood the viewport draws with.</summary>
    public const int ViewTab = 2;

    /// <summary>The outliner's Parts tab, shown in the creator.</summary>
    public const int PartsTab = 3;

    private const int UpperRow = 0;
    private const int LowerRow = 2;

    /// <summary>The context panel's tabs that have something to show, the one shown last at the end.</summary>
    private readonly List<int> _contextTabs = [];

    private readonly EditorLayout? _layout;

    /// <summary>The context panel's tab in front, or <see cref="NoTab"/>.</summary>
    [ObservableProperty]
    public partial int SideTab { get; set; } = NoTab;

    /// <summary>The outliner's tab in front.</summary>
    [ObservableProperty]
    public partial int OutlinerTab { get; set; }

    /// <summary>Whether the keyboard is in the context panel, which makes the top bar's undo and redo work on the Data tab's file.</summary>
    [ObservableProperty]
    public partial bool SidePanelFocused { get; set; }

    /// <summary>Whether the keyboard is in the outliner, which makes the top bar's undo and redo work on the View tab's mood.</summary>
    [ObservableProperty]
    public partial bool OutlinerFocused { get; set; }

    public bool ShowsInspector => _contextTabs.Contains(InspectorTab);

    public bool ShowsData => _contextTabs.Contains(DataTab);

    public bool ShowsCreator => _contextTabs.Contains(CreatorTab);

    /// <summary>Whether the context panel shows anything; when not, it says how to fill it.</summary>
    public bool HasContext => _contextTabs.Count > 0;

    /// <summary>The side column's row the context panel takes: the upper one unless the panels are swapped.</summary>
    public int ContextRow => PanelsSwapped ? LowerRow : UpperRow;

    /// <summary>The side column's row the outliner takes.</summary>
    public int OutlinerRow => PanelsSwapped ? UpperRow : LowerRow;

    private bool PanelsSwapped => _layout?.PanelsSwapped ?? _swapped;

    private bool _swapped;

    /// <summary>Puts the outliner over the context panel, or back under it, and remembers it for the next run.</summary>
    [RelayCommand]
    public void SwapPanels()
    {
        bool swapped = !PanelsSwapped;
        _swapped = swapped;
        if (_layout is not null)
        {
            _layout.PanelsSwapped = swapped;
        }

        OnPropertyChanged(nameof(ContextRow));
        OnPropertyChanged(nameof(OutlinerRow));
    }

    /// <summary>Whether the content drawer is open; closed, the viewport takes its height.</summary>
    [ObservableProperty]
    public partial bool IsContentOpen { get; set; }

    /// <summary>The height of the content drawer's row: nothing while it is closed.</summary>
    public GridLength ContentRowHeight => IsContentOpen ? new GridLength(300) : new GridLength(0);

    partial void OnIsContentOpenChanged(bool value) => OnPropertyChanged(nameof(ContentRowHeight));

    /// <summary>The commands of the command palette (Ctrl+K), each with its key.</summary>
    public IReadOnlyList<MenuChoice> PaletteChoices =>
    [
        new("Play", Play, "Run", "Play", "F5"),
        new("Compile", () => _ = Compile(), "Run", null, "Ctrl+Alt+F11"),
        new("Save", SaveShown, "File", null, "Ctrl+S"),
        new("Save assets", SaveAssets, "File", null, "Ctrl+Shift+S"),
        new("Undo", Undo, "Edit", null, "Ctrl+Z"),
        new("Redo", Redo, "Edit", null, "Ctrl+Y"),
        new("Open or close the content drawer", ToggleContent, "View", null, "Ctrl+Space"),
        new("Swap the outliner and the context panel", SwapPanels, "View"),
    ];

    /// <summary>
    /// Ctrl+S (rule R21, one way to save): saves the document shown in place of the viewport when it saves on its own, else the
    /// scene, or in the creator the creation's file, and the assets for a document saved with them.
    /// </summary>
    [RelayCommand]
    public void SaveShown()
    {
        if (Documents.Selected?.Save is { } save && save.CanExecute(null))
        {
            save.Execute(null);
            return;
        }

        if (Documents.IsLevelShown)
        {
            Viewport.Save();
            return;
        }

        SaveAssets();
    }

    /// <summary>Opens or closes the content drawer (Ctrl+Space).</summary>
    [RelayCommand]
    public void ToggleContent() => IsContentOpen = !IsContentOpen;

    /// <summary>Closes the data asset or table open in the Data tab; its unsaved edits stay held.</summary>
    [RelayCommand]
    public void CloseData()
    {
        _dataPath = null;
        DataContent = null;
        OnPropertyChanged(nameof(DataHeading));
        HideContext(DataTab);
        WatchHistory();
    }

    partial void OnSidePanelFocusedChanged(bool value) => WatchHistory();

    partial void OnOutlinerFocusedChanged(bool value) => WatchHistory();

    partial void OnSideTabChanged(int value) => WatchHistory();

    partial void OnOutlinerTabChanged(int value) => WatchHistory();

    /// <summary>Gives the context panel the tab and brings it in front.</summary>
    private void ShowContext(int tab)
    {
        _contextTabs.Remove(tab);
        _contextTabs.Add(tab);
        NotifyContext();
        SideTab = tab;
    }

    /// <summary>Takes the tab out of the context panel, bringing back the one shown before it.</summary>
    private void HideContext(int tab)
    {
        if (!_contextTabs.Remove(tab))
        {
            return;
        }

        NotifyContext();
        if (SideTab != tab)
        {
            return;
        }

        if (_contextTabs.Count > 0)
        {
            SideTab = _contextTabs[^1];
        }
        else
        {
            SideTab = NoTab;
        }
    }

    private void NotifyContext()
    {
        OnPropertyChanged(nameof(ShowsInspector));
        OnPropertyChanged(nameof(ShowsData));
        OnPropertyChanged(nameof(ShowsCreator));
        OnPropertyChanged(nameof(HasContext));
    }

    /// <summary>Shows the inspector while an entity is selected, in front when the selection changes to one.</summary>
    private void HandleSelectionChanged()
    {
        if (Viewport.Selection.Selected is not null)
        {
            ShowContext(InspectorTab);
        }
        else
        {
            HideContext(InspectorTab);
        }
    }
}
