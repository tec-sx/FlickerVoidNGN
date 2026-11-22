using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Interface.Hub.Character;
using FlickerVoid.Interface.Hub.Inventory;
using FlickerVoid.Interface.Hub.Journal;

namespace FlickerVoid.Interface.Hub;

/// <summary>
/// The hub menu over the game: three pages (Inventory, Character, Journal) of the protagonist, one shown at a time, switched in a
/// ring and reopened on the page last shown.
/// </summary>
public sealed partial class HubViewModel : ObservableObject
{
    private static readonly QueryDescription Protagonists = new QueryDescription().WithAll<Gameplay.Protagonist.Protagonist>();

    private readonly World _world;
    private readonly List<HubPageViewModel> _pages;
    private int _selectedIndex;

    public HubViewModel(Game game)
    {
        _world = game.World;
        Inventory = new InventoryPageViewModel(game);
        Character = new CharacterPageViewModel(game);
        Journal = new JournalPageViewModel(game);
        _pages = [Inventory, Character, Journal];
        SelectedPage = Inventory;
    }

    public IReadOnlyList<HubPageViewModel> Pages => _pages;

    public InventoryPageViewModel Inventory { get; }

    public CharacterPageViewModel Character { get; }

    public JournalPageViewModel Journal { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShownPage))]
    public partial bool IsOpen { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShownPage))]
    public partial HubPageViewModel SelectedPage { get; private set; }

    /// <summary>The selected page while the hub is open, else none, so a closed hub holds no page view (nor a stage being drawn).</summary>
    public HubPageViewModel? ShownPage => IsOpen ? SelectedPage : null;

    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        IsOpen = true;
        Select(_selectedIndex);
    }

    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
        SelectedPage.Hide();
    }

    [RelayCommand]
    private void NextPage() => Select((_selectedIndex + 1) % Pages.Count);

    [RelayCommand]
    private void PreviousPage() => Select((_selectedIndex + Pages.Count - 1) % Pages.Count);

    [RelayCommand]
    private void SelectPage(HubPageViewModel page)
    {
        int index = _pages.IndexOf(page);
        Select(index);
    }

    private void Select(int index)
    {
        SelectedPage.Hide();
        _selectedIndex = index;
        SelectedPage = Pages[index];
        if (IsOpen)
        {
            Entity protagonist = FindProtagonist();
            SelectedPage.Show(protagonist);
        }
    }

    private Entity FindProtagonist()
    {
        Entity found = Entity.Null;
        _world.Query(in Protagonists, (Entity entity) =>
        {
            if (found == Entity.Null)
            {
                found = entity;
            }
        });
        return found;
    }
}
