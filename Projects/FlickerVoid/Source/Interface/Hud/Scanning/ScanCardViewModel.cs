using System.Collections.ObjectModel;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;

namespace FlickerVoid.Interface.Hud.Scanning;

/// <summary>
/// The HUD's scan card while the protagonist is in scan mode: the focused scannable's title and how far its scan has come, then, once
/// scanned, at most three of its entries the protagonist can see.
/// </summary>
public sealed partial class ScanCardViewModel(ScannerSystem scanner, World world) : ObservableObject
{
    public const int MaxEntries = 3;

    public const string ScanModeTitle = "Scan mode";

    private static readonly QueryDescription Protagonists = new QueryDescription().WithAll<Gameplay.Protagonist.Protagonist, Scanner>();

    private Entity _shown = Entity.Null;
    private bool _shownScanned;

    [ObservableProperty]
    public partial bool IsVisible { get; private set; }

    [ObservableProperty]
    public partial bool HasFocus { get; private set; }

    [ObservableProperty]
    public partial string Title { get; private set; } = ScanModeTitle;

    /// <summary>From 0 to 100.</summary>
    [ObservableProperty]
    public partial double Progress { get; private set; }

    [ObservableProperty]
    public partial bool IsScanned { get; private set; }

    /// <summary>Whether the focus is still to be scanned, so its progress shows.</summary>
    [ObservableProperty]
    public partial bool ShowsProgress { get; private set; }

    public ObservableCollection<ScanEntryViewModel> Entries { get; } = [];

    public void Update()
    {
        Entity protagonist = FindProtagonist();
        IsVisible = protagonist != Entity.Null && scanner.IsActive(protagonist);
        Entity focus = Entity.Null;
        if (IsVisible)
        {
            focus = scanner.GetFocus(protagonist);
        }

        HasFocus = focus != Entity.Null;
        float progress = scanner.GetProgress(protagonist);
        Progress = progress * 100;
        bool scanned = scanner.WasScanned(focus);
        IsScanned = scanned;
        ShowsProgress = HasFocus && !scanned;
        if (focus == _shown && scanned == _shownScanned)
        {
            return;
        }

        _shown = focus;
        _shownScanned = scanned;
        Title = FindTitle(focus);
        Entries.Clear();
        if (!scanned)
        {
            return;
        }

        IReadOnlyList<ScanEntry> entries = scanner.GetVisibleEntries(protagonist, focus);
        foreach (ScanEntry entry in entries.Take(MaxEntries))
        {
            Entries.Add(new ScanEntryViewModel(entry.Label, entry.Text));
        }
    }

    private string FindTitle(Entity focus)
    {
        if (focus == Entity.Null)
        {
            return ScanModeTitle;
        }

        ScanDefinition? definition = scanner.GetDefinition(focus);
        if (definition is not null && definition.Display.Name.Length > 0)
        {
            return definition.Display.Name;
        }

        if (world.TryGet(focus, out EntityName name))
        {
            return name.Value;
        }

        return string.Empty;
    }

    private Entity FindProtagonist()
    {
        Entity found = Entity.Null;
        world.Query(in Protagonists, (Entity entity) =>
        {
            if (found == Entity.Null)
            {
                found = entity;
            }
        });
        return found;
    }
}
