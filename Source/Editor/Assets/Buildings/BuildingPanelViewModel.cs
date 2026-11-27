using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Assets.Creators;
using FlickerVoid.Engine.Buildings;

namespace FlickerVoid.Editor.Assets.Buildings;

/// <summary>
/// The building creator's panel: how many storeys stand on the ground, a cellar or none, stairs from a storey to the one above, and
/// an elevator, each choice written to the file through the document.
/// </summary>
public sealed partial class BuildingPanelViewModel : CreatorPanelViewModel
{
    private readonly BuildingDocument _document;
    private bool _showing;

    public BuildingPanelViewModel(BuildingDocument document)
        : base(document)
    {
        _document = document;
        Show();
    }

    public override string Kind => "Building";

    public IReadOnlyList<int> StoreyCounts { get; } = [.. Enumerable.Range(1, BuildingFile.MaxStoreys)];

    /// <summary>How many storeys stand on the ground, the ground floor among them.</summary>
    [ObservableProperty]
    public partial int Storeys { get; set; }

    [ObservableProperty]
    public partial bool HasCellar { get; set; }

    /// <summary>The parts stairs can climb from: every storey with one above it.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> StairsFrom { get; private set; } = [];

    /// <summary>The storey the next flight of stairs climbs from.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddStairsCommand))]
    public partial string? StairsStorey { get; set; }

    [ObservableProperty]
    public partial bool HasElevator { get; set; }

    /// <summary>Adds a flight from the chosen storey to the one above and opens that floor over it.</summary>
    [RelayCommand(CanExecute = nameof(CanAddStairs))]
    public void AddStairs()
    {
        BuildingStorey? from = _document.Building.Storeys.Find(storey => BuildingFile.GetStoreyId(storey.Level) == StairsStorey);
        if (from is null)
        {
            return;
        }

        _document.AddStairs(from.Level);
        Show();
    }

    private bool CanAddStairs() => StairsStorey is not null;

    // A count the building doesn't take, such as one typed in, is put back.
    partial void OnStoreysChanged(int value)
    {
        if (!_showing && StoreyCounts.Contains(value) && value != _document.Building.StoreysAbove)
        {
            _document.SetStoreys(value, _document.Building.HasCellar);
        }

        Show();
    }

    partial void OnHasCellarChanged(bool value)
    {
        if (!_showing && value != _document.Building.HasCellar)
        {
            _document.SetStoreys(_document.Building.StoreysAbove, value);
        }

        Show();
    }

    partial void OnHasElevatorChanged(bool value)
    {
        bool has = _document.Building.Elevator is not null;
        if (!_showing && value && !has)
        {
            _document.AddElevator();
        }
        else if (!_showing && !value && has)
        {
            _document.RemoveElevator();
        }

        Show();
    }

    private void Show()
    {
        if (_showing)
        {
            return;
        }

        _showing = true;
        BuildingFile building = _document.Building;
        Storeys = building.StoreysAbove;
        HasCellar = building.HasCellar;
        HasElevator = building.Elevator is not null;
        string? chosen = StairsStorey;
        IEnumerable<BuildingStorey> below = building.Storeys.Take(building.Storeys.Count - 1);
        StairsFrom = [.. below.Select(static storey => BuildingFile.GetStoreyId(storey.Level))];
        if (chosen is not null && StairsFrom.Contains(chosen))
        {
            StairsStorey = chosen;
        }
        else
        {
            StairsStorey = StairsFrom.FirstOrDefault();
        }

        ShowParts();
        _showing = false;
    }
}
