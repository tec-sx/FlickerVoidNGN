using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Assets.Creators;
using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;

namespace FlickerVoid.Editor.Assets.Buildings;

/// <summary>
/// The building creator's panel: the storeys (top first, the one being edited picked, added above, a cellar, duplicated, taken away)
/// and the building's parts as a tree, each storey's floor, walls, rooms, openings and pillars, then its stairs, elevators, shots and
/// roof, each edit written to the file through the document.
/// </summary>
public sealed partial class BuildingPanelViewModel : CreatorPanelViewModel
{
    private readonly string _name;
    private bool _showing;

    public BuildingPanelViewModel(BuildingDocument document)
        : base(document)
    {
        Document = document;
        _name = Path.GetFileName(document.Path);
        Show();
    }

    public override string Kind => "Building";

    public BuildingDocument Document { get; }

    /// <summary>The building's storeys, the top one first, as the storey switcher stacks them.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<BuildingStoreyItem> Storeys { get; private set; } = [];

    /// <summary>The storey being edited.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveStoreyCommand))]
    public partial BuildingStoreyItem? Storey { get; set; }

    /// <summary>Whether the storeys above and below the one edited are shown in the viewport; off, the plan shows the edited storey alone.</summary>
    [ObservableProperty]
    public partial bool ShowsOthers { get; set; }

    /// <summary>The building's parts as a tree, flattened in order with each line's depth.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<BuildingNode> Nodes { get; private set; } = [];

    /// <summary>The level of the storey being edited; the ground floor's when none is picked.</summary>
    public int Level => Storey?.Level ?? 0;

    /// <summary>Raised when another storey is picked, so the plan tools work on it.</summary>
    public event Action<int>? LevelChanged;

    /// <summary>Shows the storey at the level as the one being edited.</summary>
    public void ShowLevel(int level)
    {
        BuildingStoreyItem? item = Storeys.FirstOrDefault(storey => storey.Level == level);
        if (item is not null && item != Storey)
        {
            Storey = item;
        }
    }

    /// <summary>Adds a storey on top, its plan a copy of the top storey's walls, and edits it.</summary>
    [RelayCommand]
    public void AddStoreyAbove()
    {
        int level = 0;
        Document.Change(building => level = BuildingTools.AddStoreyAbove(building));
        Show();
        ShowLevel(level);
    }

    /// <summary>Gives the building a cellar under the ground floor, when it has none, and edits it.</summary>
    [RelayCommand]
    public void AddCellar()
    {
        if (!Document.Building.HasCellar)
        {
            Document.Change(static building => BuildingTools.AddCellar(building));
            Show();
        }

        ShowLevel(-1);
    }

    /// <summary>Adds a copy of the storey being edited on top and edits it.</summary>
    [RelayCommand]
    public void DuplicateStorey()
    {
        int from = Level;
        int level = from;
        Document.Change(building => level = BuildingTools.DuplicateStorey(building, from));
        Show();
        ShowLevel(level);
    }

    /// <summary>Takes the storey being edited away; the ground floor stays.</summary>
    [RelayCommand(CanExecute = nameof(CanRemoveStorey))]
    public void RemoveStorey()
    {
        int level = Level;
        Document.Change(building => BuildingTools.RemoveStorey(building, level));
        Show();
        int below = Math.Max(level - 1, Document.Building.Storeys[0].Level);
        if (level < 0)
        {
            below = 0;
        }

        ShowLevel(below);
    }

    private bool CanRemoveStorey() => Level != 0;

    /// <summary>Reads the storeys and the parts from the file again, after an edit.</summary>
    public void Refresh() => Show();

    partial void OnStoreyChanged(BuildingStoreyItem? value)
    {
        if (!_showing && value is not null)
        {
            LevelChanged?.Invoke(value.Level);
        }
    }

    private void Show()
    {
        if (_showing)
        {
            return;
        }

        _showing = true;
        BuildingFile building = Document.Building;
        int level = Level;
        List<BuildingStoreyItem> storeys = [];
        for (int index = building.Storeys.Count - 1; index >= 0; index--)
        {
            int at = building.Storeys[index].Level;
            storeys.Add(new BuildingStoreyItem(at, BuildingToolViewModel.GetLevelName(at)));
        }

        Storeys = storeys;
        Storey = storeys.FirstOrDefault(storey => storey.Level == level) ?? storeys.FirstOrDefault(static storey => storey.Level == 0);
        Nodes = GetNodes(building);
        ShowParts();
        _showing = false;
        RemoveStoreyCommand.NotifyCanExecuteChanged();
    }

    private List<BuildingNode> GetNodes(BuildingFile building)
    {
        HashSet<string> made = [.. building.GetParts().Select(static part => part.Id)];
        List<BuildingNode> nodes = [new BuildingNode(_name, "Building", 0, null, null)];
        foreach (BuildingStorey storey in building.Storeys)
        {
            AddStorey(nodes, building, storey, made);
        }

        if (!building.Roof.None)
        {
            nodes.Add(new BuildingNode("Roof", "Roof", 1, Made(made, BuildingFile.RoofId), new PlanPick(PlanPickKind.Roof, building.Storeys[^1].Level, 0)));
        }

        return nodes;
    }

    private static void AddStorey(List<BuildingNode> nodes, BuildingFile building, BuildingStorey storey, HashSet<string> made)
    {
        int level = storey.Level;
        string title = BuildingToolViewModel.GetLevelName(level);
        if (level > 0)
        {
            title = $"Storey {level}";
        }

        string storeyId = BuildingFile.GetStoreyId(level);
        nodes.Add(new BuildingNode(title, "Layers", 1, null, new PlanPick(PlanPickKind.Storey, level, 0)));
        nodes.Add(new BuildingNode("Floor", "Floor", 2, Made(made, $"{storeyId}.floor"), new PlanPick(PlanPickKind.Storey, level, 0)));
        List<PlanWall> outside = [.. storey.Walls.Where(static wall => wall.Outside)];
        if (outside.Count > 0)
        {
            nodes.Add(new BuildingNode("Outside walls", "Walls", 2, Made(made, $"{storeyId}.walls"), new PlanPick(PlanPickKind.Wall, level, outside[0].Id)));
        }

        int number = 0;
        foreach (PlanWall wall in storey.Walls.Where(static wall => !wall.Outside))
        {
            number++;
            nodes.Add(new BuildingNode($"Wall {number}", "Wall", 2, Made(made, $"{storeyId}.wall.{wall.Id}"), new PlanPick(PlanPickKind.Wall, level, wall.Id)));
        }

        foreach (PlanRoom room in storey.Rooms)
        {
            nodes.Add(new BuildingNode(room.Name, "Room", 2, null, new PlanPick(PlanPickKind.Room, level, 0, room.Name)));
        }

        foreach (PlanOpening opening in storey.Openings)
        {
            string icon = GetOpeningIcon(opening.Kind);
            string part = $"{storeyId}.door.{opening.Id}";
            if (opening.Kind != OpeningKind.Door)
            {
                part = $"{storeyId}.frame.{opening.Id}";
            }

            nodes.Add(new BuildingNode(opening.Name, icon, 2, Made(made, part), new PlanPick(PlanPickKind.Opening, level, opening.Id)));
        }

        foreach (PlanPillar pillar in storey.Pillars)
        {
            nodes.Add(new BuildingNode($"Pillar {pillar.Id}", "Pillar", 2, Made(made, $"{storeyId}.pillar.{pillar.Id}"), new PlanPick(PlanPickKind.Pillar, level, pillar.Id)));
        }

        foreach (BuildingStairs stairs in building.Stairs.Where(stairs => stairs.Level == level))
        {
            nodes.Add(new BuildingNode($"Stairs {stairs.Id}", "Steps", 2, Made(made, BuildingFile.GetStairsId(stairs)), new PlanPick(PlanPickKind.Stairs, level, stairs.Id)));
        }

        foreach (BuildingElevator elevator in building.Elevators.Where(elevator => elevator.From == level))
        {
            nodes.Add(new BuildingNode($"Elevator {elevator.Id}", "Elevator", 2, Made(made, BuildingFile.GetElevatorId(elevator)), new PlanPick(PlanPickKind.Elevator, level, elevator.Id)));
        }

        foreach (BuildingShot shot in building.Shots.Where(shot => shot.Level == level))
        {
            nodes.Add(new BuildingNode(shot.Name, "Camera", 2, null, new PlanPick(PlanPickKind.Shot, level, shot.Id)));
        }
    }

    private static string GetOpeningIcon(OpeningKind kind)
    {
        switch (kind)
        {
            case OpeningKind.Door:
                return "Door";
            case OpeningKind.Window:
                return "Window";
            default:
                return "Square";
        }
    }

    private static string? Made(HashSet<string> made, string id)
    {
        if (made.Contains(id))
        {
            return id;
        }

        return null;
    }
}
