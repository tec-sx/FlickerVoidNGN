using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Assets.Buildings.Rows;
using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Editor.Assets.Buildings;

/// <summary>
/// The building creator's Tool tab: the tool in hand with what it works on, in folding sections of numbers, switches and choices that
/// edit the building at once (joining an edit that waits for Apply), lists of what follows, and a line of help. It shows the picked
/// wall, corner, room, pillar, opening, stairs, elevator, roof or shot, or the settings the tool places new ones with.
/// </summary>
public sealed partial class BuildingToolViewModel : ObservableObject
{
    private static readonly string[] Levels = ["Cellar", "Ground"];

    private readonly BuildingEditor _editor;
    private readonly Func<BuildingFile?> _file;
    private readonly Func<(Vector3 Position, Quaternion Rotation)?> _view;
    private readonly Action<BuildingShot> _lookThrough;
    private int _trigger;
    private float _pushed;

    /// <param name="view">Where the viewport's camera stands in the building's space, for Set from view.</param>
    /// <param name="lookThrough">Shows the viewport through the shot.</param>
    public BuildingToolViewModel(BuildingEditor editor, Func<BuildingFile?> file, Func<(Vector3 Position, Quaternion Rotation)?> view, Action<BuildingShot> lookThrough)
    {
        _editor = editor;
        _file = file;
        _view = view;
        _lookThrough = lookThrough;
        _editor.Changed += Refresh;
        Refresh();
    }

    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Subtitle { get; private set; } = string.Empty;

    /// <summary>The tool's icon's name.</summary>
    [ObservableProperty]
    public partial string Icon { get; private set; } = "Select";

    /// <summary>The key that takes the tool, shown in the tab's corner.</summary>
    [ObservableProperty]
    public partial string Key { get; private set; } = string.Empty;

    public ObservableCollection<ToolSection> Sections { get; } = [];

    [ObservableProperty]
    public partial string Help { get; private set; } = string.Empty;

    /// <summary>Whether an edit waits for Apply or Cancel.</summary>
    public bool IsPending => _editor.IsPending;

    public string PendingName => _editor.PendingName;

    [RelayCommand]
    public void Apply() => _editor.Apply();

    [RelayCommand]
    public void Cancel() => _editor.Cancel();

    /// <summary>Takes the picked thing out of the building.</summary>
    [RelayCommand]
    public void Remove()
    {
        if (_editor.Picked is not { } pick)
        {
            return;
        }

        _editor.Apply();
        Change($"Remove {pick.Kind}", building => RemovePick(building, pick));
        _editor.Pick(null);
        _editor.Apply();
    }

    /// <summary>Builds the sections again for the tool and the pick, keeping the rows that stay so a number being dragged keeps its box.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(PendingName));
        if (_file() is not { } file)
        {
            Sections.Clear();
            return;
        }

        if (!_editor.IsPending)
        {
            _pushed = 0;
        }

        List<ToolSection> sections = [];
        BuildingTool tool = _editor.Tool;
        PlanPick? pick = _editor.Picked;
        BuildingStorey? storey = file.FindStorey(_editor.Level);
        (Title, Icon, Key, Help) = Describe(tool);
        Subtitle = DescribePick(file, pick);
        switch (pick?.Kind)
        {
            case PlanPickKind.Wall when storey is not null && PlanGeometry.FindWall(storey, pick.Id) is { } wall:
                AddWall(sections, file, storey, wall);
                break;
            case PlanPickKind.Corner when storey is not null && PlanGeometry.FindCorner(storey, pick.Id) is { } corner:
                AddCorner(sections, corner);
                break;
            case PlanPickKind.Room when storey is not null && storey.Rooms.Find(room => room.Name == pick.Room) is { } room:
                AddRoom(sections, file, storey, room);
                break;
            case PlanPickKind.Pillar when storey is not null && storey.Pillars.Find(pillar => pillar.Id == pick.Id) is { } pillar:
                AddPillar(sections, pillar);
                break;
            case PlanPickKind.Opening when storey is not null && storey.Openings.Find(opening => opening.Id == pick.Id) is { } opening:
                AddOpening(sections, file, storey, opening);
                break;
            case PlanPickKind.Stairs when file.Stairs.Find(stairs => stairs.Id == pick.Id) is { } stairs:
                AddStairs(sections, file, stairs);
                break;
            case PlanPickKind.Elevator when file.Elevators.Find(elevator => elevator.Id == pick.Id) is { } elevator:
                AddElevator(sections, file, elevator);
                break;
            case PlanPickKind.Shot when file.Shots.Find(shot => shot.Id == pick.Id) is { } shot:
                AddShot(sections, file, shot);
                break;
            case PlanPickKind.Roof:
                AddRoof(sections, file);
                break;
            default:
                AddNew(sections, file, storey, tool);
                break;
        }

        if (tool == BuildingTool.Camera)
        {
            AddShotList(sections, file);
        }

        Merge(sections);
    }

    private void Merge(List<ToolSection> sections)
    {
        bool same = sections.Count == Sections.Count;
        for (int index = 0; same && index < sections.Count; index++)
        {
            same = Sections[index].Matches(sections[index]);
        }

        if (same)
        {
            for (int index = 0; index < sections.Count; index++)
            {
                Sections[index].Take(sections[index]);
            }

            return;
        }

        Sections.Clear();
        foreach (ToolSection section in sections)
        {
            Sections.Add(section);
        }
    }

    /// <summary>Edits the building: joining the edit that waits for Apply, or as an undo step of its own.</summary>
    private void Change(string name, Action<BuildingFile> edit)
    {
        bool pending = _editor.IsPending;
        _editor.Edit(name, edit);
        if (!pending)
        {
            _editor.Apply();
        }
    }

    private static (string Title, string Icon, string Key, string Help) Describe(BuildingTool tool)
    {
        switch (tool)
        {
            case BuildingTool.PushWall:
                return ("Push wall", "PushWall", "", "Drag a wall along its normal. Its neighbours stretch, its thickness stays, and the floor and the rooms are made again. Enter applies, Esc cancels.");
            case BuildingTool.MoveCorner:
                return ("Move corner", "MoveCorner", "", "Drag a corner; every wall meeting it follows. Points snap to the grid.");
            case BuildingTool.SplitWall:
                return ("Split wall", "SplitWall", "", "Click a wall to put a corner in it there. Each half keeps the openings standing on it.");
            case BuildingTool.Arc:
                return ("Arc", "Arc", "", "Drag a wall's middle sideways to bend it into an arc; drag it back onto the line to straighten it.");
            case BuildingTool.Wall:
                return ("Wall", "Wall", "", "Drag from one point to another. Ends join the corners and walls they meet, and walls crossed are split, so rooms close.");
            case BuildingTool.Room:
                return ("Room", "Room", "", "Drag a rectangle: four inside walls close a new room, joining the walls they meet.");
            case BuildingTool.Pillar:
                return ("Pillar", "Pillar", "", "Click to stand a pillar in a room. Corner pillars stand at every outside corner of the storey.");
            case BuildingTool.Opening:
                return ("Opening", "Door", "D", "Until Apply the opening stays loose: drag it along the wall or up it with the green arrow, or onto another wall. Red shows where it no longer fits.");
            case BuildingTool.Stairs:
                return ("Stairs", "Steps", "S", "Click the floor to place a flight from this storey to the one above; drag it to move it. The floor above is opened over it, with a railing round the opening.");
            case BuildingTool.Elevator:
                return ("Elevator", "Elevator", "T", "Click the floor to place a shaft; it stops at every storey it spans, with a call button on each floor.");
            case BuildingTool.Roof:
                return ("Roof", "Roof", "O", "The roof over the top storey: flat, a shed's slope or a gable.");
            case BuildingTool.Camera:
                return ("Camera", "Camera", "Y", "Click to drop a shot at the view. With a shot picked, Add trigger and click a room, a door or stairs.");
            default:
                return ("Select", "Select", "Q", "Click a wall, corner, room, pillar, opening, stairs, elevator or shot to see it here.");
        }
    }

    private static string DescribePick(BuildingFile file, PlanPick? pick)
    {
        if (pick is null)
        {
            return "Nothing picked";
        }

        string storey = GetLevelName(pick.Level);
        switch (pick.Kind)
        {
            case PlanPickKind.Wall:
                BuildingStorey? onStorey = file.FindStorey(pick.Level);
                PlanWall? wall = onStorey is null ? null : PlanGeometry.FindWall(onStorey, pick.Id);
                string kind = wall?.Outside == true ? "Outside wall" : "Inside wall";
                return $"{kind} {pick.Id}, {storey}";
            case PlanPickKind.Room:
                return $"{pick.Room}, {storey}";
            case PlanPickKind.Opening:
                string? name = file.FindStorey(pick.Level)?.Openings.Find(opening => opening.Id == pick.Id)?.Name;
                return $"{name}, {storey}";
            case PlanPickKind.Shot:
                string? shot = file.Shots.Find(found => found.Id == pick.Id)?.Name;
                return $"{shot}, {storey}";
            case PlanPickKind.Roof:
                return "Over the top storey";
            default:
                return $"{pick.Kind} {pick.Id}, {storey}";
        }
    }

    /// <summary>A storey's name in the editor: Cellar, Ground, then 1, 2 and up.</summary>
    public static string GetLevelName(int level)
    {
        if (level < 0)
        {
            return Levels[0];
        }

        if (level == 0)
        {
            return Levels[1];
        }

        return level.ToString(CultureInfo.InvariantCulture);
    }

    private void AddWall(List<ToolSection> sections, BuildingFile file, BuildingStorey storey, PlanWall wall)
    {
        int level = storey.Level;
        int id = wall.Id;
        if (_editor.Tool == BuildingTool.PushWall)
        {
            ToolRow distance = new NumberRow("Distance", _pushed, "m", value => Push(level, id, value), step: 0.1);
            ToolRow snap = new ToggleRow("Snap to grid", _editor.SnapOn, on => _editor.SnapOn = on);
            ToolRow carry = new ToggleRow("Carry openings", _editor.CarryOpenings, on => _editor.CarryOpenings = on);
            sections.Add(new ToolSection("Push wall", "PushWall", [distance, snap, carry]));
        }

        if (_editor.Tool == BuildingTool.Arc)
        {
            ToolRow bulge = new NumberRow("Bulge", wall.Bulge, "m", value => Change("Arc", building => PlanEdits.SetBulge(Storey(building, level), id, value)), step: 0.1);
            ToolRow segments = new NumberRow("Segments", wall.Segments, "", value => Change("Arc segments", building => Wall(building, level, id).Segments = Math.Clamp((int)value, 2, 48)), 2, 48, 1);
            ToolRow straighten = new ActionsRow("", [new ToolAction("Straighten", "Line", () => Change("Straighten", building => PlanEdits.SetBulge(Storey(building, level), id, 0)))]);
            sections.Add(new ToolSection("Arc", "Arc", [bulge, segments, straighten]));
        }

        List<Vector2> path = PlanGeometry.GetPath(storey, wall);
        float length = PlanGeometry.GetLength(path);
        float full = storey.Height - storey.Floor;
        ToolRow lengthRow = new NumberRow("Length", length, "m", value => Change("Wall length", building => SetLength(Storey(building, level), id, value)), 0.2, step: 0.1);
        ToolRow thickness = new NumberRow("Thickness", PlanGeometry.GetThickness(storey, wall), "m", value => Change("Wall thickness", building => Wall(building, level, id).Thickness = Math.Max(value, 0.02f)), 0.02, 2, 0.01);
        ToolRow height = new NumberRow("Height", PlanGeometry.GetHeight(storey, wall), "m", value => Change("Wall height", building => SetHeight(Wall(building, level, id), value, full)), 0.1, full, 0.05);
        ToolRow outside = new ToggleRow("Outside", wall.Outside, on => Change("Outside wall", building => Wall(building, level, id).Outside = on));
        ToolRow remove = new ActionsRow("", [new ToolAction("Remove wall", "Delete", Remove)]);
        sections.Add(new ToolSection("This wall", "Line", [lengthRow, thickness, height, outside, remove]));

        List<ToolRow> follows = [];
        foreach (int corner in new[] { wall.From, wall.To })
        {
            foreach (PlanWall other in PlanGeometry.GetWallsAt(storey, corner))
            {
                if (other.Id == wall.Id || follows.Exists(row => row.Label == WallName(other)))
                {
                    continue;
                }

                float otherLength = PlanGeometry.GetLength(PlanGeometry.GetPath(storey, other));
                follows.Add(new ListRow(WallName(other), $"{Format(otherLength)} m", "Line", () => _editor.Pick(new PlanPick(PlanPickKind.Wall, level, other.Id))));
            }
        }

        follows.Add(new ListRow("Floor", "rebuilt", "Floor"));
        foreach (RoomShape room in PlanGeometry.FindRooms(storey))
        {
            if (room.Record >= 0)
            {
                string name = storey.Rooms[room.Record].Name;
                follows.Add(new ListRow(name, $"{room.Area.ToString("0.0", CultureInfo.InvariantCulture)} m²", "Room", () => _editor.Pick(new PlanPick(PlanPickKind.Room, level, 0, name))));
            }
        }

        sections.Add(new ToolSection("Follows", "Network", follows));
    }

    private static string WallName(PlanWall wall) => wall.Outside ? $"Outside wall {wall.Id}" : $"Wall {wall.Id}";

    private void Push(int level, int id, float value)
    {
        float step = value - _pushed;
        _pushed = value;
        if (MathF.Abs(step) < 1e-5f)
        {
            return;
        }

        bool carry = _editor.CarryOpenings;
        _editor.Edit($"Push wall · {value.ToString("+0.00;-0.00", CultureInfo.InvariantCulture)} m", building => PlanEdits.PushWall(Storey(building, level), id, step, carry));
    }

    private static void SetLength(BuildingStorey storey, int id, float length)
    {
        PlanWall wall = PlanGeometry.FindWall(storey, id)!;
        PlanCorner from = PlanGeometry.FindCorner(storey, wall.From)!;
        PlanCorner to = PlanGeometry.FindCorner(storey, wall.To)!;
        Vector2 start = PlanGeometry.GetPoint(from);
        Vector2 direction = PlanGeometry.GetPoint(to) - start;
        if (direction.LengthSquared() < 1e-8f)
        {
            return;
        }

        Vector2 end = start + (Vector2.Normalize(direction) * Math.Max(length, 0.2f));
        PlanEdits.MoveCorner(storey, to.Id, end);
    }

    private static void SetHeight(PlanWall wall, float height, float full)
    {
        if (height >= full - 1e-3f)
        {
            wall.Height = WallHeight.ToCeiling;
            return;
        }

        wall.Height = WallHeight.Custom;
        wall.CustomHeight = height;
    }

    private void AddCorner(List<ToolSection> sections, PlanCorner corner)
    {
        int level = _editor.Level;
        int id = corner.Id;
        ToolRow x = new NumberRow("X", corner.X, "m", value => Change("Move corner", building => MoveCorner(building, level, id, value, null)), step: 0.1);
        ToolRow z = new NumberRow("Z", corner.Z, "m", value => Change("Move corner", building => MoveCorner(building, level, id, null, value)), step: 0.1);
        ToolRow remove = new ActionsRow("", [new ToolAction("Remove corner", "Delete", Remove)]);
        sections.Add(new ToolSection("Corner", "MoveCorner", [x, z, remove]));
    }

    private static void MoveCorner(BuildingFile building, int level, int id, float? x, float? z)
    {
        BuildingStorey storey = Storey(building, level);
        PlanCorner corner = PlanGeometry.FindCorner(storey, id)!;
        Vector2 point = new(x ?? corner.X, z ?? corner.Z);
        PlanEdits.MoveCorner(storey, id, point);
    }

    private void AddRoom(List<ToolSection> sections, BuildingFile file, BuildingStorey storey, PlanRoom room)
    {
        int level = storey.Level;
        string name = room.Name;
        ToolRow rename = new TextRow("Name", room.Name, value => RenameRoom(level, name, value));
        ToolRow ambient = new NumberRow("Ambient", room.Ambient, "", value => Change("Room light", building => FindRoom(building, level, name)!.Ambient = Math.Clamp(value, 0, 1)), 0, 1, 0.05);
        List<ToolRow> rows = [rename, ambient];
        foreach (RoomShape shape in PlanGeometry.FindRooms(storey))
        {
            if (shape.Record >= 0 && storey.Rooms[shape.Record] == room)
            {
                rows.Insert(1, new ListRow("Floor area", $"{shape.Area.ToString("0.0", CultureInfo.InvariantCulture)} m²", "Floor"));
            }
        }

        sections.Add(new ToolSection("Room", "Room", rows));
        List<ToolRow> shots = [];
        foreach (BuildingShot shot in file.Shots)
        {
            if (shot.Triggers.Exists(trigger => trigger.Target == TriggerTarget.Room && trigger.Level == level && trigger.Room == name))
            {
                shots.Add(new ListRow(shot.Name, "on entering", "Camera", () => _editor.Pick(new PlanPick(PlanPickKind.Shot, shot.Level, shot.Id))));
            }
        }

        if (shots.Count == 0)
        {
            shots.Add(new ListRow("No shot", "Camera tool adds one", "Warning", isWarning: true));
        }

        sections.Add(new ToolSection("Camera", "Camera", shots));
    }

    private void RenameRoom(int level, string old, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name == old)
        {
            return;
        }

        Change("Rename room", building =>
        {
            if (FindRoom(building, level, old) is { } room)
            {
                room.Name = name;
            }

            foreach (ShotTrigger trigger in building.Shots.SelectMany(shot => shot.Triggers))
            {
                if (trigger.Target == TriggerTarget.Room && trigger.Level == level && trigger.Room == old)
                {
                    trigger.Room = name;
                }
            }
        });
        _editor.Pick(new PlanPick(PlanPickKind.Room, level, 0, name));
    }

    private static PlanRoom? FindRoom(BuildingFile building, int level, string name) => building.FindStorey(level)?.Rooms.Find(room => room.Name == name);

    private void AddPillar(List<ToolSection> sections, PlanPillar pillar)
    {
        int level = _editor.Level;
        int id = pillar.Id;
        ToolRow x = new NumberRow("X", pillar.X, "m", value => Change("Move pillar", building => Pillar(building, level, id).X = value), step: 0.1);
        ToolRow z = new NumberRow("Z", pillar.Z, "m", value => Change("Move pillar", building => Pillar(building, level, id).Z = value), step: 0.1);
        ToolRow size = new NumberRow("Size", pillar.Size, "m", value => Change("Pillar size", building => Pillar(building, level, id).Size = Math.Max(value, 0.05f)), 0.05, 4, 0.05);
        ToolRow round = new ChoiceRow("Shape", ["Square", "Round"], pillar.Round ? 1 : 0, index => Change("Pillar shape", building => Pillar(building, level, id).Round = index == 1));
        ToolRow remove = new ActionsRow("", [new ToolAction("Remove pillar", "Delete", Remove)]);
        sections.Add(new ToolSection("Pillar", "Pillar", [x, z, size, round, remove]));
    }

    private static PlanPillar Pillar(BuildingFile building, int level, int id) => Storey(building, level).Pillars.Find(pillar => pillar.Id == id)!;

    private void AddOpening(List<ToolSection> sections, BuildingFile file, BuildingStorey storey, PlanOpening opening)
    {
        int level = storey.Level;
        int id = opening.Id;
        string name = opening.Name;
        ToolRow kind = new ChoiceRow("Kind", ["Door", "Doorway", "Window", "Floor"], (int)opening.Kind, index => SetKind(level, id, (OpeningKind)index));
        if (opening.Kind == OpeningKind.Floor)
        {
            ToolRow holeX = new NumberRow("X", opening.X, "m", value => Change(name, building => Opening(building, level, id).X = value), step: 0.1);
            ToolRow holeZ = new NumberRow("Z", opening.Z, "m", value => Change(name, building => Opening(building, level, id).Z = value), step: 0.1);
            ToolRow width = new NumberRow("Width", opening.Width, "m", value => Change(name, building => Opening(building, level, id).Width = Math.Max(value, 0.2f)), 0.2, 20, 0.05);
            ToolRow depth = new NumberRow("Depth", opening.Height, "m", value => Change(name, building => Opening(building, level, id).Height = Math.Max(value, 0.2f)), 0.2, 20, 0.05);
            ToolRow drop = new ActionsRow("", [new ToolAction("Remove hole", "Delete", Remove)]);
            sections.Add(new ToolSection("Opening", "Door", [kind, new TextRow("Name", name, value => Change("Rename", building => Opening(building, level, id).Name = value))]));
            sections.Add(new ToolSection("Hole", "Floor", [holeX, holeZ, width, depth, drop]));
            return;
        }

        ToolRow top = new ChoiceRow("Top", ["Flat", "Arch"], (int)opening.Top, index => Change(name, building => Opening(building, level, id).Top = (OpeningTop)index));
        ToolRow rise = new NumberRow("Arch rise", opening.ArchRise, "m", value => Change(name, building => Opening(building, level, id).ArchRise = Math.Max(value, 0.01f)), 0.01, 5, 0.05);
        ToolRow rename = new TextRow("Name", name, value => Change("Rename", building => Opening(building, level, id).Name = value));
        sections.Add(new ToolSection("Opening", "Door", [kind, top, rise, rename]));

        float length = 0;
        if (PlanGeometry.FindWall(storey, opening.Wall) is { } wall)
        {
            length = PlanGeometry.GetLength(PlanGeometry.GetPath(storey, wall));
        }

        ToolRow along = new NumberRow("Along the wall", opening.Along, "m", value => Change(name, building => Opening(building, level, id).Along = value), 0, length, 0.05);
        ToolRow lift = new NumberRow("Lift", opening.Lift, "m", value => Change(name, building => Opening(building, level, id).Lift = Math.Max(value, 0)), 0, 20, 0.05);
        ToolAction floor = new("On the floor", "Down", () => Change(name, building => Opening(building, level, id).Lift = 0));
        ToolAction centre = new("Centre on wall", "Fit", () => Change(name, building => Opening(building, level, id).Along = length / 2));
        sections.Add(new ToolSection("Place", "Move", [along, lift, new ActionsRow("", [floor, centre])]));

        ToolRow widthRow = new NumberRow("Width", opening.Width, "m", value => Change(name, building => Opening(building, level, id).Width = Math.Max(value, 0.1f)), 0.1, 20, 0.05);
        ToolRow heightRow = new NumberRow("Height", opening.Height, "m", value => Change(name, building => Opening(building, level, id).Height = Math.Max(value, 0.1f)), 0.1, 20, 0.05);
        ToolRow thickness = new NumberRow("Thickness", opening.Thickness, "m", value => Change(name, building => Opening(building, level, id).Thickness = Math.Max(value, 0.01f)), 0.01, 1, 0.01);
        sections.Add(new ToolSection("Size", "Scale", [widthRow, heightRow, thickness]));

        if (opening.Kind == OpeningKind.Door)
        {
            ToolRow hinge = new ChoiceRow("Hinge", ["Left", "Right"], opening.HingeLeft ? 0 : 1, index => Change(name, building => Opening(building, level, id).HingeLeft = index == 0));
            ToolRow opens = new ChoiceRow("Opens", ["Out", "In"], opening.Outward ? 0 : 1, index => Change(name, building => Opening(building, level, id).Outward = index == 0));
            ToolRow angle = new NumberRow("Angle", opening.Angle, "°", value => Change(name, building => Opening(building, level, id).Angle = Math.Clamp(value, 1, 179)), 1, 179, 1);
            ToolRow locked = new ToggleRow("Locked", opening.Locked, on => Change(name, building => Opening(building, level, id).Locked = on));
            ToolRow key = new TextRow("Key tag", opening.Key.IsValid ? opening.Key.ToString() : string.Empty, value => Change(name, building => Opening(building, level, id).Key = ToTag(value)));
            ToolRow consume = new ToggleRow("Key is used up", opening.ConsumeKey, on => Change(name, building => Opening(building, level, id).ConsumeKey = on));
            ToolRow pickable = new ToggleRow("Can be picked", opening.Pickable, on => Change(name, building => Opening(building, level, id).Pickable = on));
            ToolRow difficulty = new NumberRow("Difficulty", opening.Difficulty, "", value => Change(name, building => Opening(building, level, id).Difficulty = Math.Clamp(value, 0, 1)), 0, 1, 0.05);
            sections.Add(new ToolSection("Leaf", "Door", [hinge, opens, angle, locked, key, consume, pickable, difficulty], isExpanded: false));
        }

        ToolRow frame = new NumberRow("Frame width", opening.Frame, "m", value => Change(name, building => Opening(building, level, id).Frame = Math.Max(value, 0)), 0, 1, 0.01);
        List<ToolRow> frameRows = [frame];
        if (opening.Kind == OpeningKind.Window)
        {
            frameRows.Add(new ToggleRow("Glass", opening.Glass, on => Change(name, building => Opening(building, level, id).Glass = on)));
        }

        sections.Add(new ToolSection("Frame and window", "Window", frameRows, isExpanded: false));
        ToolRow margin = new NumberRow("Margin", opening.Margin, "m", value => Change(name, building => Opening(building, level, id).Margin = Math.Max(value, 0)), 0, 0.5, 0.01);
        ToolRow fits = new ListRow("Fits its wall", OpeningShape.Fits(file, storey, opening) ? "yes" : "no: move it or make it smaller", OpeningShape.Fits(file, storey, opening) ? "Check" : "Warning", isWarning: !OpeningShape.Fits(file, storey, opening));
        ToolRow removeOpening = new ActionsRow("", [new ToolAction("Remove opening", "Delete", Remove)]);
        sections.Add(new ToolSection("Cut", "Cut", [margin, fits, removeOpening], isExpanded: false));
    }

    private void SetKind(int level, int id, OpeningKind kind)
    {
        Change("Opening kind", building =>
        {
            BuildingStorey storey = Storey(building, level);
            PlanOpening opening = storey.Openings.Find(found => found.Id == id)!;
            if (kind == OpeningKind.Floor && opening.Kind != OpeningKind.Floor && PlanGeometry.FindWall(storey, opening.Wall) is { } wall)
            {
                (Vector2 point, _) = PlanGeometry.GetAlong(PlanGeometry.GetPath(storey, wall), opening.Along);
                Vector2 inward = -PlanEdits.GetNormal(storey, wall);
                Vector2 hole = point + (inward * 1.2f);
                opening.X = hole.X;
                opening.Z = hole.Y;
                opening.Width = 1.2f;
                opening.Height = 1.2f;
            }

            if (kind == OpeningKind.Window && opening.Kind != OpeningKind.Window)
            {
                opening.Lift = 0.9f;
                opening.Height = 1.2f;
            }
            else if (kind != OpeningKind.Window && opening.Kind == OpeningKind.Window)
            {
                opening.Lift = 0;
                opening.Height = 2.1f;
            }

            opening.Kind = kind;
            PlanEdits.KeepOpenings(storey);
        });
        _editor.NewOpening = kind;
    }

    private static Tag ToTag(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return default;
        }

        return new Tag(trimmed);
    }

    private static PlanOpening Opening(BuildingFile building, int level, int id) => Storey(building, level).Openings.Find(opening => opening.Id == id)!;

    private void AddStairs(List<ToolSection> sections, BuildingFile file, BuildingStairs stairs)
    {
        int id = stairs.Id;
        StairsLayout layout = BuildingMaker.GetStairsLayout(file, stairs);
        ToolRow kind = new ChoiceRow("Kind", ["Straight", "L", "U"], (int)stairs.Kind, index => Change("Stairs", building => Stairs(building, id).Kind = (StairsKind)index));
        ToolRow turn = new ChoiceRow("Turn", ["Left", "Right"], stairs.TurnLeft ? 0 : 1, index => Change("Stairs", building => Stairs(building, id).TurnLeft = index == 0));
        ToolRow heading = new NumberRow("Heading", stairs.Heading, "°", value => Change("Stairs", building => Stairs(building, id).Heading = value), -360, 360, 15);
        ToolRow width = new NumberRow("Width", stairs.Width, "m", value => Change("Stairs", building => Stairs(building, id).Width = Math.Max(value, 0.5f)), 0.5, 5, 0.05);
        ToolRow first = new NumberRow("First run", stairs.FirstRun, "m", value => Change("Stairs", building => Stairs(building, id).FirstRun = Math.Max(value, 0.3f)), 0.3, 20, 0.1);
        ToolRow landing = new NumberRow("Landing", stairs.Landing, "m", value => Change("Stairs", building => Stairs(building, id).Landing = Math.Max(value, 0.5f)), 0.5, 5, 0.05);
        ToolRow rise = new NumberRow("Rise", stairs.Rise, "m", value => Change("Stairs", building => Stairs(building, id).Rise = Math.Clamp(value, 0.1f, 0.3f)), 0.1, 0.3, 0.01);
        ToolRow tread = new NumberRow("Tread", stairs.Tread, "m", value => Change("Stairs", building => Stairs(building, id).Tread = Math.Clamp(value, 0.15f, 0.6f)), 0.15, 0.6, 0.01);
        ToolRow count = new ListRow("Count", $"{layout.Steps} steps of {layout.StepRise.ToString("0.000", CultureInfo.InvariantCulture)} m", "Steps");
        ToolRow meets = new ToggleRow("Meets the floor", stairs.MeetsFloor, on => Change("Stairs", building => Stairs(building, id).MeetsFloor = on));
        sections.Add(new ToolSection("Stairs", "Steps", [kind, turn, heading, width, first, landing, rise, tread, count, meets]));
        ToolRow margin = new NumberRow("Margin", stairs.Margin, "m", value => Change("Stairs opening", building => Stairs(building, id).Margin = Math.Max(value, 0)), 0, 1, 0.01);
        ToolRow headroom = new NumberRow("Headroom", stairs.Headroom, "m", value => Change("Stairs opening", building => Stairs(building, id).Headroom = Math.Max(value, 1.5f)), 1.5, 4, 0.05);
        ToolRow railing = new ToggleRow("Railing", stairs.Railing, on => Change("Stairs railing", building => Stairs(building, id).Railing = on));
        sections.Add(new ToolSection("Opening above", "Floor", [margin, headroom, railing]));
        List<ToolRow> cameras = [];
        foreach (BuildingShot shot in file.Shots)
        {
            if (shot.Triggers.Exists(trigger => trigger.Target == TriggerTarget.Stairs && trigger.Id == id))
            {
                cameras.Add(new ListRow(shot.Name, "on the stairs", "Camera", () => _editor.Pick(new PlanPick(PlanPickKind.Shot, shot.Level, shot.Id))));
            }
        }

        cameras.Add(new ActionsRow("", [new ToolAction("Add a shot of the stairs", "Camera", () => AddStairsShot(file, stairs)), new ToolAction("Remove stairs", "Delete", Remove)]));
        sections.Add(new ToolSection("Camera", "Camera", cameras));
    }

    private void AddStairsShot(BuildingFile file, BuildingStairs stairs)
    {
        if (BuildingRegions.GetStairs(file, stairs.Id) is not { } region)
        {
            return;
        }

        Vector3 middle = Vector3.Transform(region.Middle, region.Place.ToMatrix());
        (Vector3 position, Quaternion rotation) = BuildingTools.LookAt(middle, float.DegreesToRadians(stairs.Heading + 135), -0.6f, BuildingEditor.ShotDistance);
        int id = stairs.Id;
        int level = stairs.Level;
        int shotId = 1 + file.Shots.Select(static shot => shot.Id).DefaultIfEmpty(0).Max();
        Change("Stairs shot", building =>
        {
            int added = BuildingTools.AddShot(building, level, position, rotation);
            BuildingShot shot = building.Shots.Find(found => found.Id == added)!;
            shot.Name = $"Stairs {id} shot";
            shot.Triggers.Add(new ShotTrigger { Target = TriggerTarget.Stairs, Level = level, Id = id, When = TriggerMoment.Both });
        });
        _editor.Pick(new PlanPick(PlanPickKind.Shot, level, shotId));
    }

    private static BuildingStairs Stairs(BuildingFile building, int id) => building.Stairs.Find(stairs => stairs.Id == id)!;

    private void AddElevator(List<ToolSection> sections, BuildingFile file, BuildingElevator elevator)
    {
        int id = elevator.Id;
        List<string> levels = [.. file.Storeys.Select(static storey => GetLevelName(storey.Level))];
        int lowest = file.Storeys[0].Level;
        ToolRow from = new ChoiceRow("From", levels, elevator.From - lowest, index => Change("Elevator", building => Elevator(building, id).From = lowest + index));
        ToolRow to = new ChoiceRow("To", levels, elevator.To - lowest, index => Change("Elevator", building => Elevator(building, id).To = lowest + index));
        ToolRow size = new NumberRow("Size", elevator.Size, "m", value => Change("Elevator", building => Elevator(building, id).Size = Math.Max(value, 0.8f)), 0.8, 6, 0.1);
        ToolRow heading = new NumberRow("Heading", elevator.Heading, "°", value => Change("Elevator", building => Elevator(building, id).Heading = value), -360, 360, 90);
        ToolRow speed = new NumberRow("Speed", elevator.Speed, "m/s", value => Change("Elevator", building => Elevator(building, id).Speed = Math.Max(value, 0.1f)), 0.1, 10, 0.1);
        ToolRow calls = new ToggleRow("Call buttons", elevator.CallButtons, on => Change("Elevator", building => Elevator(building, id).CallButtons = on));
        ToolRow railing = new ToggleRow("Railing", elevator.Railing, on => Change("Elevator", building => Elevator(building, id).Railing = on));
        string span = $"{GetLevelName(elevator.From)} to {GetLevelName(elevator.To)}";
        string buttons = elevator.CallButtons ? "call button on each floor" : "no call buttons";
        ToolRow summary = new ListRow(span, buttons, "Elevator");
        ToolRow remove = new ActionsRow("", [new ToolAction("Remove elevator", "Delete", Remove)]);
        sections.Add(new ToolSection("Elevator", "Elevator", [summary, from, to, size, heading, speed, calls, railing, remove]));
    }

    private static BuildingElevator Elevator(BuildingFile building, int id) => building.Elevators.Find(elevator => elevator.Id == id)!;

    private void AddRoof(List<ToolSection> sections, BuildingFile file)
    {
        BuildingRoof roof = file.Roof;
        ToolRow kind = new ChoiceRow("Kind", ["Flat", "Shed", "Gable"], (int)roof.Kind, index => Change("Roof", building => building.Roof.Kind = (RoofKind)index));
        ToolRow rise = new NumberRow("Rise", roof.Rise, "m", value => Change("Roof", building => building.Roof.Rise = Math.Max(value, 0.05f)), 0.05, 10, 0.1);
        ToolRow overhang = new NumberRow("Overhang", roof.Overhang, "m", value => Change("Roof", building => building.Roof.Overhang = Math.Max(value, 0)), 0, 3, 0.05);
        ToolRow thickness = new NumberRow("Thickness", roof.Thickness, "m", value => Change("Roof", building => building.Roof.Thickness = Math.Max(value, 0.02f)), 0.02, 1, 0.01);
        ToolRow along = new ChoiceRow("Ridge along", ["Width", "Depth"], roof.AlongDepth ? 1 : 0, index => Change("Roof", building => building.Roof.AlongDepth = index == 1));
        ToolRow none = new ToggleRow("No roof", roof.None, on => Change("Roof", building => building.Roof.None = on));
        sections.Add(new ToolSection("Roof", "Roof", [kind, rise, overhang, thickness, along, none]));
    }

    private void AddShot(List<ToolSection> sections, BuildingFile file, BuildingShot shot)
    {
        int id = shot.Id;
        ToolRow name = new TextRow("Name", shot.Name, value => Change("Shot name", building => Shot(building, id).Name = value));
        ToolRow place = new VectorRow("Place", shot.Position, value => Change("Shot place", building => Shot(building, id).Position = value));
        ToolRow size = new NumberRow("Size", shot.Size, "m", value => Change("Shot size", building => Shot(building, id).Size = Math.Max(value, 0.5f)), 0.5, 100, 0.1);
        ToolRow follow = new NumberRow("Follow", shot.Follow, "", value => Change("Shot follow", building => Shot(building, id).Follow = Math.Clamp(value, 0, 1)), 0, 1, 0.01);
        ToolRow look = new NumberRow("Look", shot.Look, "m", value => Change("Shot look", building => Shot(building, id).Look = Math.Max(value, 0)), 0, 5, 0.01);
        ToolAction through = new("Look through", "Maximize", () => LookThrough(id));
        ToolAction fromView = new("Set from view", "Camera", () => SetFromView(id));
        sections.Add(new ToolSection("Shot", "Camera", [name, place, size, follow, look, new ActionsRow("", [through, fromView])]));

        List<ToolRow> triggers = [];
        _trigger = Math.Clamp(_trigger, 0, Math.Max(shot.Triggers.Count - 1, 0));
        for (int index = 0; index < shot.Triggers.Count; index++)
        {
            ShotTrigger trigger = shot.Triggers[index];
            int picked = index;
            string change = trigger.Change == TriggerChange.Cut ? "cut" : $"{trigger.Change.ToString().ToLowerInvariant()} {trigger.Time.ToString("0.0", CultureInfo.InvariantCulture)} s";
            triggers.Add(new ListRow(DescribeTrigger(file, trigger), change, TriggerIcon(trigger), () => PickTrigger(picked), isChosen: index == _trigger));
        }

        triggers.Add(new ActionsRow("", [new ToolAction(_editor.AddingTrigger ? "Click a room, door or stairs" : "Add trigger: click a room, door or stairs", "Trigger", StartTrigger, accent: true)]));
        sections.Add(new ToolSection("Triggers", "Trigger", triggers));
        if (shot.Triggers.Count > 0)
        {
            ShotTrigger trigger = shot.Triggers[_trigger];
            int index = _trigger;
            ToolRow when = new ChoiceRow("When", ["Enter", "Leave", "Both"], (int)trigger.When, value => Change("Trigger", building => Shot(building, id).Triggers[index].When = (TriggerMoment)value));
            ToolRow changeRow = new ChoiceRow("Change", ["Cut", "Blend", "Fade"], (int)trigger.Change, value => Change("Trigger", building => Shot(building, id).Triggers[index].Change = (TriggerChange)value));
            ToolRow time = new NumberRow("Time", trigger.Time, "s", value => Change("Trigger", building => Shot(building, id).Triggers[index].Time = Math.Max(value, 0)), 0, 10, 0.05);
            ToolRow back = new ChoiceRow("Back to", ["The room entered", "The shot before"], (int)trigger.Return, value => Change("Trigger", building => Shot(building, id).Triggers[index].Return = (TriggerReturn)value));
            ToolRow drop = new ActionsRow("", [new ToolAction("Remove trigger", "Delete", () => Change("Remove trigger", building => Shot(building, id).Triggers.RemoveAt(index)))]);
            sections.Add(new ToolSection("Picked trigger", "Room", [when, changeRow, time, back, drop]));
        }

        sections.Add(new ToolSection("This shot", "Camera", [new ActionsRow("", [new ToolAction("Remove shot", "Delete", Remove)])], isExpanded: false));
    }

    private void LookThrough(int id)
    {
        if (_file() is { } file && file.Shots.Find(shot => shot.Id == id) is { } shot)
        {
            _lookThrough(shot);
        }
    }

    private void PickTrigger(int index)
    {
        _trigger = index;
        Refresh();
    }

    private void StartTrigger()
    {
        _editor.AddingTrigger = true;
        Refresh();
    }

    private void SetFromView(int id)
    {
        if (_view() is not { } view)
        {
            return;
        }

        Change("Shot from view", building =>
        {
            BuildingShot shot = Shot(building, id);
            shot.Position = view.Position;
            shot.Rotation = view.Rotation;
        });
    }

    private static string TriggerIcon(ShotTrigger trigger)
    {
        switch (trigger.Target)
        {
            case TriggerTarget.Opening:
                return "Door";
            case TriggerTarget.Stairs:
                return "Steps";
            case TriggerTarget.Elevator:
                return "Elevator";
            default:
                return "Room";
        }
    }

    /// <summary>What a trigger sets off on, as its list names it: Room: Hall, Front door, Stairs 1.</summary>
    public static string DescribeTrigger(BuildingFile file, ShotTrigger trigger)
    {
        switch (trigger.Target)
        {
            case TriggerTarget.Opening:
                string? opening = file.FindStorey(trigger.Level)?.Openings.Find(found => found.Id == trigger.Id)?.Name;
                return opening ?? $"Opening {trigger.Id} (gone)";
            case TriggerTarget.Stairs:
                return $"Stairs {trigger.Id}";
            case TriggerTarget.Elevator:
                return $"Elevator {trigger.Id}";
            default:
                return $"Room: {trigger.Room}";
        }
    }

    private static BuildingShot Shot(BuildingFile building, int id) => building.Shots.Find(shot => shot.Id == id)!;

    private void AddShotList(List<ToolSection> sections, BuildingFile file)
    {
        List<ToolRow> rows = [];
        foreach (BuildingShot shot in file.Shots)
        {
            string count = shot.Triggers.Count == 1 ? "1 trigger" : $"{shot.Triggers.Count} triggers";
            bool chosen = _editor.Picked is { Kind: PlanPickKind.Shot } pick && pick.Id == shot.Id;
            rows.Add(new ListRow(shot.Name, count, "Camera", () => _editor.Pick(new PlanPick(PlanPickKind.Shot, shot.Level, shot.Id)), isChosen: chosen));
        }

        foreach (BuildingStairs stairs in file.Stairs)
        {
            bool covered = file.Shots.Exists(shot => shot.Triggers.Exists(trigger => trigger.Target == TriggerTarget.Stairs && trigger.Id == stairs.Id));
            if (!covered)
            {
                rows.Add(new ListRow($"Stairs {stairs.Id}", "no shot", "Warning", () => _editor.Pick(new PlanPick(PlanPickKind.Stairs, stairs.Level, stairs.Id)), isWarning: true));
            }
        }

        if (rows.Count == 0)
        {
            rows.Add(new NoteRow("No shots yet: click the floor to drop one."));
        }

        sections.Add(new ToolSection("Building", "Building", rows));
    }

    /// <summary>With nothing picked: the settings the tool places new things with, and the storey's own.</summary>
    private void AddNew(List<ToolSection> sections, BuildingFile file, BuildingStorey? storey, BuildingTool tool)
    {
        switch (tool)
        {
            case BuildingTool.Wall:
                sections.Add(new ToolSection("New walls", "Wall", [new ToggleRow("Outside", _editor.WallOutside, on => _editor.WallOutside = on), new ToggleRow("Snap to grid", _editor.SnapOn, on => _editor.SnapOn = on)]));
                break;
            case BuildingTool.Pillar:
                ToolRow size = new NumberRow("Size", _editor.PillarSize, "m", value => _editor.PillarSize = Math.Max(value, 0.05f), 0.05, 4, 0.05);
                ToolRow round = new ChoiceRow("Shape", ["Square", "Round"], _editor.PillarRound ? 1 : 0, index => _editor.PillarRound = index == 1);
                sections.Add(new ToolSection("New pillars", "Pillar", [size, round]));
                break;
            case BuildingTool.Opening:
                ToolRow kind = new ChoiceRow("Kind", ["Door", "Doorway", "Window", "Floor"], (int)_editor.NewOpening, index => _editor.NewOpening = (OpeningKind)index);
                sections.Add(new ToolSection("New opening", "Door", [kind]));
                break;
            case BuildingTool.Stairs:
                ToolRow stairsKind = new ChoiceRow("Kind", ["Straight", "L", "U"], (int)_editor.NewStairs, index => _editor.NewStairs = (StairsKind)index);
                ToolRow heading = new NumberRow("Heading", _editor.StairsHeading, "°", value => _editor.StairsHeading = value, -360, 360, 90);
                sections.Add(new ToolSection("New stairs", "Steps", [stairsKind, heading]));
                break;
            case BuildingTool.Roof:
                AddRoof(sections, file);
                return;
            default:
                break;
        }

        if (storey is null || tool is BuildingTool.Camera or BuildingTool.Elevator)
        {
            return;
        }

        int level = storey.Level;
        ToolRow height = new NumberRow("Height", storey.Height, "m", value => Change("Storey height", building => Storey(building, level).Height = Math.Max(value, 1.5f)), 1.5, 20, 0.1);
        ToolRow wall = new NumberRow("Wall", storey.Wall, "m", value => Change("Storey walls", building => Storey(building, level).Wall = Math.Max(value, 0.02f)), 0.02, 2, 0.01);
        ToolRow floor = new NumberRow("Floor", storey.Floor, "m", value => Change("Storey floor", building => Storey(building, level).Floor = Math.Clamp(value, 0.02f, 1)), 0.02, 1, 0.01);
        ToolRow ambient = new NumberRow("Ambient", storey.Ambient, "", value => Change("Storey light", building => Storey(building, level).Ambient = Math.Clamp(value, 0, 1)), 0, 1, 0.05);
        ToolRow corners = new ToggleRow("Corner pillars", storey.CornerPillars, on => Change("Corner pillars", building => Storey(building, level).CornerPillars = on));
        ToolRow cornerSize = new NumberRow("Corner pillar size", storey.CornerPillarSize, "m", value => Change("Corner pillars", building => Storey(building, level).CornerPillarSize = Math.Max(value, 0.1f)), 0.1, 3, 0.05);
        sections.Add(new ToolSection($"Storey: {GetLevelName(level)}", "Layers", [height, wall, floor, ambient, corners, cornerSize]));
    }

    private static BuildingStorey Storey(BuildingFile building, int level) => building.FindStorey(level)!;

    private static PlanWall Wall(BuildingFile building, int level, int id) => PlanGeometry.FindWall(Storey(building, level), id)!;

    private static void RemovePick(BuildingFile building, PlanPick pick)
    {
        BuildingStorey? storey = building.FindStorey(pick.Level);
        switch (pick.Kind)
        {
            case PlanPickKind.Wall when storey is not null:
                PlanEdits.RemoveWall(storey, pick.Id);
                break;
            case PlanPickKind.Corner when storey is not null:
                PlanEdits.RemoveCorner(storey, pick.Id);
                break;
            case PlanPickKind.Pillar when storey is not null:
                storey.Pillars.RemoveAll(pillar => pillar.Id == pick.Id);
                break;
            case PlanPickKind.Opening when storey is not null:
                storey.Openings.RemoveAll(opening => opening.Id == pick.Id);
                foreach (BuildingShot shot in building.Shots)
                {
                    shot.Triggers.RemoveAll(trigger => trigger.Target == TriggerTarget.Opening && trigger.Level == pick.Level && trigger.Id == pick.Id);
                }

                break;
            case PlanPickKind.Stairs:
                building.Stairs.RemoveAll(stairs => stairs.Id == pick.Id);
                break;
            case PlanPickKind.Elevator:
                building.Elevators.RemoveAll(elevator => elevator.Id == pick.Id);
                break;
            case PlanPickKind.Shot:
                building.Shots.RemoveAll(shot => shot.Id == pick.Id);
                break;
            case PlanPickKind.Roof:
                building.Roof.None = true;
                break;
            default:
                break;
        }
    }

    private static string Format(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
