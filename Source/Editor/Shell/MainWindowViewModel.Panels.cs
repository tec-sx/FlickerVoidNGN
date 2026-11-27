using Arch.Core;

using Avalonia.Controls;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Core.Menus;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Persistence.Layout;
using FlickerVoid.Editor.Viewports;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor;

public sealed partial class MainWindowViewModel
{
    /// <summary>The context panel's index when it shows nothing.</summary>
    public const int NoTab = -1;

    /// <summary>The context panel's Inspector tab, shown while an entity is selected.</summary>
    public const int InspectorTab = 0;

    /// <summary>The context panel's Data tab, shown while a data asset or table is open.</summary>
    public const int DataTab = 1;

    /// <summary>
    /// The context panel's Tool tab, shown while a creator's mesh is entered or a building is made: the options of the tool in hand,
    /// before the creator's own tab as the building mockups have it.
    /// </summary>
    public const int ToolTab = 2;

    /// <summary>The context panel's creator tab, shown while a vehicle or a building is made.</summary>
    public const int CreatorTab = 3;

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

    public bool ShowsTool => _contextTabs.Contains(ToolTab);

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

    /// <summary>Whether the content drawer is open; closed, the viewport takes its height. Remembered for the next run.</summary>
    [ObservableProperty]
    public partial bool IsContentOpen { get; set; }

    /// <summary>The content drawer's height, dragged by its top edge and remembered for the next run.</summary>
    [ObservableProperty]
    public partial double ContentHeight { get; set; } = EditorLayout.DefaultContentHeight;

    /// <summary>The Day dock's height, dragged by its top edge and remembered for the next run.</summary>
    [ObservableProperty]
    public partial double DayDockHeight { get; set; } = EditorLayout.DefaultDayDockHeight;

    /// <summary>The side column's width, dragged by its left edge and remembered for the next run.</summary>
    [ObservableProperty]
    public partial GridLength SideWidth { get; set; } = new(EditorLayout.DefaultSideWidth);

    /// <summary>Takes the drawer's state and the panels' sizes the last run left.</summary>
    private void RestoreLayout()
    {
        if (_layout is null)
        {
            return;
        }

        IsContentOpen = _layout.ContentOpen;
        ContentHeight = _layout.ContentHeight;
        DayDockHeight = _layout.DayDockHeight;
        SideWidth = new GridLength(_layout.SideWidth);
    }

    partial void OnIsContentOpenChanged(bool value)
    {
        if (_layout is not null && _layout.ContentOpen != value)
        {
            _layout.ContentOpen = value;
        }
    }

    partial void OnContentHeightChanged(double value)
    {
        if (_layout is not null && _layout.ContentHeight != value)
        {
            _layout.ContentHeight = value;
        }
    }

    partial void OnDayDockHeightChanged(double value)
    {
        if (_layout is not null && _layout.DayDockHeight != value)
        {
            _layout.DayDockHeight = value;
        }
    }

    partial void OnSideWidthChanged(GridLength value)
    {
        if (_layout is not null && value.IsAbsolute && _layout.SideWidth != value.Value)
        {
            _layout.SideWidth = value.Value;
        }
    }

    /// <summary>
    /// What the command palette (Ctrl+K) offers: the commands, each with its key, and, found only by searching, every content file
    /// (opened as a double-click would) and every entity of the level (selected).
    /// </summary>
    public IReadOnlyList<MenuChoice> PaletteChoices
    {
        get
        {
            List<MenuChoice> choices =
            [
                new("Play", Play, "Commands", "Play", "F5"),
                new("Compile", () => _ = Compile(), "Commands", "Code", "Ctrl+Alt+F11"),
                new("Save", SaveShown, "Commands", "Save", "Ctrl+S"),
                new("Save assets", SaveAssets, "Commands", "SaveAssets", "Ctrl+Shift+S"),
                new("Undo", Undo, "Commands", "Undo", "Ctrl+Z"),
                new("Redo", Redo, "Commands", "Redo", "Ctrl+Y"),
                new("Edit the day's light", ToggleDayDock, "Commands", "Sun", Detail: "opens the Day dock"),
                new("Bake light probes", () => Viewport.BakeProbesCommand.Execute(null), "Commands", "Bake"),
                new("Open or close the content drawer", ToggleContent, "View", "Folder", "Ctrl+Space"),
                new("Swap the outliner and the context panel", SwapPanels, "View", "Swap"),
            ];
            foreach (AssetEntry asset in Assets.FindFiles())
            {
                string path = asset.Path;
                string? folder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                string file = System.IO.Path.GetFileName(path);
                choices.Add(new MenuChoice(file, () => OpenContentFile(path), "Content", asset.Type.Icon, Detail: folder, SearchOnly: true));
            }

            World world = Viewport.Scene.Game.World;
            foreach (Entity entity in Viewport.Scene.Entities)
            {
                if (!world.TryGet(entity, out EntityName name))
                {
                    continue;
                }

                Entity found = entity;
                string icon = SceneIcons.For(world, entity, Viewport.Scene.Game.Spawner.Components);
                string? place = world.TryGet(entity, out EntityFolder filed) ? filed.Path.Replace("/", " / ") : null;
                choices.Add(new MenuChoice(name.Value, () => Viewport.Scene.Selection.Select(found), "Entities", icon, Detail: place, SearchOnly: true));
            }

            return choices;
        }
    }

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
        OnPropertyChanged(nameof(DataTitle));
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
        OnPropertyChanged(nameof(ShowsTool));
        OnPropertyChanged(nameof(HasContext));
    }

    /// <summary>The Tool tab comes in front while the mesh is entered or the door tool is in hand, and goes when neither is.</summary>
    private void HandleMeshToolsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MeshToolsViewModel.HasToolOptions))
        {
            return;
        }

        bool wanted = Viewport.MeshTools.HasToolOptions || Viewport.Building is not null;
        if (wanted == ShowsTool)
        {
            return;
        }

        if (wanted)
        {
            ShowContext(ToolTab);
        }
        else
        {
            HideContext(ToolTab);
        }
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
