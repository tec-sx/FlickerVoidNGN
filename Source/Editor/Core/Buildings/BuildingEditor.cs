using System.Globalization;
using System.Numerics;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Gizmos;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Editor.Core.Buildings;

/// <summary>
/// The building creator's tools in the viewport, on the storey being edited: a click anywhere on a wall, an opening, a pillar, stairs
/// or an elevator takes it, and drags push walls, move corners, split and bend walls, draw walls and rooms, stand pillars, place and
/// slide openings and their handles, stairs and elevators, and drop camera shots and their triggers; with the select tool the gizmo
/// moves, turns and sizes what is picked. Each edit is held as one undo step that waits, as the confirm bar shows, until it is applied
/// or cancelled; the next edit applies it. What can't be done is said through <see cref="Warned"/>. Draws the plan and labels it.
/// </summary>
public sealed partial class BuildingEditor : ICreatorTool
{
    /// <summary>How near a click must come to a wall, corner or opening to take it, in metres.</summary>
    public const float Reach = 0.45f;

    /// <summary>How far back a dropped shot stands from the point it looks at, in metres.</summary>
    public const float ShotDistance = 14;

    /// <summary>How near the mouse must come to a handle on the screen to take it, in pixels.</summary>
    public const float GrabPixels = 12;

    /// <summary>How far a new flight must be dragged from its foot before the drag turns it, in metres.</summary>
    private const float TurnFrom = 0.3f;

    /// <summary>The steps a heading snaps to, in degrees.</summary>
    private const float TurnStep = 15;

    private readonly EditorCamera _camera;
    private readonly Func<Matrix4x4> _place;
    private readonly Func<BuildingFile?> _file;
    private readonly Action<string, Action<BuildingFile>> _change;
    private readonly UndoHistory _history;
    private readonly Dictionary<string, float> _before = [];
    private readonly List<Vector3[]> _ghost = [];
    private BuildingTool _tool;
    private int _level;
    private Drag _drag;
    private Vector2 _dragStart;
    private Vector2 _dragEnd;
    private Vector2 _grabOffset;
    private Vector2 _pixelStart;
    private float _dragValue;
    private float _dragHeight;
    private float _grab;
    private int _width = 1;
    private int _height = 1;
    private bool _grouped;
    private string? _joins;

    /// <param name="place">The building's place in the world, as its root stands.</param>
    /// <param name="change">Edits the building under a name, then makes its parts again and holds it as an undo step.</param>
    public BuildingEditor(EditorCamera camera, Func<Matrix4x4> place, Func<BuildingFile?> file, Action<string, Action<BuildingFile>> change, UndoHistory history)
    {
        _camera = camera;
        _place = place;
        _file = file;
        _change = change;
        _history = history;
    }

    private enum Drag
    {
        None,
        Push,
        Corner,
        Arc,
        Draw,
        Rectangle,
        Along,
        Lift,
        Handle,
        Pillar,
        Stairs,
        StairsHeading,
        StairsWidth,
        StairsRun,
        Elevator,
        ElevatorSize,
        Gizmo,
    }

    /// <summary>Raised when the tool, the storey, the pick, the hover or a pending edit changes.</summary>
    public event Action? Changed;

    /// <summary>Raised with what to tell the user when an edit can't be made, such as stairs with no storey above.</summary>
    public event Action<string>? Warned;

    public BuildingTool Tool
    {
        get => _tool;
        set
        {
            if (_tool == value)
            {
                return;
            }

            Apply();
            _tool = value;
            AddingTrigger = false;
            if (value != BuildingTool.Select)
            {
                Gizmo.Mode = GizmoMode.Select;
            }

            if (Picked is { } pick && !Keeps(value, pick.Kind))
            {
                Picked = null;
            }

            Changed?.Invoke();
        }
    }

    /// <summary>The level of the storey being edited; the others are shown faint.</summary>
    public int Level
    {
        get => _level;
        set
        {
            if (_level == value)
            {
                return;
            }

            Apply();
            _level = value;
            Picked = null;
            Changed?.Invoke();
        }
    }

    /// <summary>Whether points and distances snap to <see cref="Snap"/>.</summary>
    public bool SnapOn { get; set; } = true;

    /// <summary>Whether a wall being drawn snaps its ends to the walls and corners near them.</summary>
    public bool SnapToWalls { get; set; } = true;

    /// <summary>Whether a wall being drawn snaps its ends to the middles of the walls near them.</summary>
    public bool SnapToMiddles { get; set; } = true;

    /// <summary>Whether openings on the walls a pushed wall stretches move with it, rather than keep their place.</summary>
    public bool CarryOpenings { get; set; } = true;

    /// <summary>The grid points and distances snap to, in metres.</summary>
    public float Snap { get; set; } = 0.1f;

    /// <summary>What was picked last: the thing the Tool tab shows.</summary>
    public PlanPick? Picked { get; private set; }

    /// <summary>What the tool would take under the mouse.</summary>
    public PlanPick? Hover { get; private set; }

    /// <summary>The kind of opening the opening tool places.</summary>
    public OpeningKind NewOpening { get; set; }

    /// <summary>The kind of stairs the stairs tool places, and the way they climb (degrees from +Z toward +X).</summary>
    public StairsKind NewStairs { get; set; }

    public float StairsHeading { get; set; }

    /// <summary>Whether the wall tool draws outside walls, which the floor is laid under, rather than inside ones.</summary>
    public bool WallOutside { get; set; }

    public float PillarSize { get; set; } = 0.4f;

    public bool PillarRound { get; set; }

    /// <summary>Whether the next click with the camera tool adds a trigger to the picked shot.</summary>
    public bool AddingTrigger { get; set; }

    /// <summary>Whether an edit waits for Apply or Cancel.</summary>
    public bool IsPending => _grouped;

    /// <summary>What the pending edit is, as the confirm bar names it: "Push wall · +1.20 m".</summary>
    public string PendingName { get; private set; } = string.Empty;

    /// <summary>How far the pending edit has pushed the picked wall.</summary>
    public float Pushed { get; private set; }

    /// <summary>The storey's wall lengths (<c>wall.{id}</c>) and room areas (<c>room.{name}</c>) from before the pending edit.</summary>
    public IReadOnlyDictionary<string, float> Before => _before;

    /// <summary>
    /// Whether the storey is shown open, as the stairs and elevator tools need it: its walls only as their lines on the floor, the
    /// floor above see-through with what will be cut out of it.
    /// </summary>
    public bool ShowsOpenStorey => !ShowsWhole && _tool is BuildingTool.Stairs or BuildingTool.Elevator;

    /// <summary>Whether the whole building stands under its roof, with no storey opened: while the roof is picked, or its tool is in hand with nothing else picked.</summary>
    public bool ShowsWhole => Picked is { Kind: PlanPickKind.Roof } || (_tool == BuildingTool.Roof && Picked is null);

    /// <summary>Picks something of the building, as a click in the Parts tab does.</summary>
    public void Pick(PlanPick? pick)
    {
        Picked = pick;
        if (pick is not null && pick.Kind != PlanPickKind.Roof && pick.Kind != PlanPickKind.Elevator)
        {
            _level = pick.Level;
        }

        Changed?.Invoke();
    }

    /// <summary>Keeps the pending edit.</summary>
    public void Apply()
    {
        if (!_grouped)
        {
            return;
        }

        Settle();
        _history.EndGroup();
        Changed?.Invoke();
    }

    /// <summary>Puts the building back as it was before the pending edit.</summary>
    public void Cancel()
    {
        if (!_grouped)
        {
            return;
        }

        Settle();
        _history.CancelGroup();
        Changed?.Invoke();
    }

    /// <summary>Pushes a wall a step further along its normal, as one step of the pending edit.</summary>
    public void Push(int level, int id, float step)
    {
        Pushed += step;
        bool carry = CarryOpenings;
        string name = $"Push wall · {Format(Pushed, "+0.00;-0.00")} m";
        Edit(name, building => PlanEdits.PushWall(building.FindStorey(level)!, id, step, carry));
    }

    /// <summary>Edits the building as one step of the pending edit, which starts with it when none waits.</summary>
    public void Edit(string name, Action<BuildingFile> edit)
    {
        if (!_grouped)
        {
            Remember();
            _history.BeginGroup(name);
            _grouped = true;
        }

        PendingName = name;
        _change(name, edit);
        Changed?.Invoke();
    }

    /// <summary>Tells the user why what they tried was not done.</summary>
    public void Warn(string message) => Warned?.Invoke(message);

    public bool Press(Vector2 pixel, int width, int height)
    {
        _width = width;
        _height = height;
        if (_file() is not { } file || file.FindStorey(_level) is not { } storey || !TryGetRay(pixel, out Ray ray))
        {
            return false;
        }

        float floor = file.GetFloorHeight(_level);
        _pixelStart = pixel;
        _dragValue = 0;
        _dragHeight = floor;
        _joins = null;
        if (PressGizmo(file, storey, pixel) || PressHandle(file, storey, pixel, ray))
        {
            return true;
        }

        PlanHit? hit = FindHit(file, storey, ray, pixel, _tool);
        bool onFloor = TryGetPlanePoint(ray, floor, out Vector2 point);
        if (!onFloor && hit is null)
        {
            return false;
        }

        if (!onFloor)
        {
            point = new Vector2(hit!.Value.Point.X, hit.Value.Point.Z);
        }

        if (_tool == BuildingTool.Select)
        {
            Picked = hit?.Pick;
            Changed?.Invoke();
            return false;
        }

        if (_tool is not (BuildingTool.Opening or BuildingTool.Stairs or BuildingTool.Elevator or BuildingTool.Camera or BuildingTool.Pillar))
        {
            Apply();
        }

        _dragStart = point;
        _dragEnd = point;
        switch (_tool)
        {
            case BuildingTool.PushWall:
                return BeginOnWall(storey, hit, point, Drag.Push);
            case BuildingTool.Arc:
                return BeginOnWall(storey, hit, point, Drag.Arc);
            case BuildingTool.MoveCorner:
                return PressCorner(storey, hit, ray, pixel, floor);
            case BuildingTool.SplitWall:
                return PressSplit(storey, hit, point);
            case BuildingTool.Wall:
                _dragStart = SnapWallEnd(storey, point);
                _dragEnd = _dragStart;
                _drag = Drag.Draw;
                return true;
            case BuildingTool.Room:
                _dragStart = SnapPoint(point);
                _dragEnd = _dragStart;
                _drag = Drag.Rectangle;
                return true;
            case BuildingTool.Pillar:
                return PressPillar(storey, hit, point);
            case BuildingTool.Opening:
                return PressOpening(file, storey, hit, point);
            case BuildingTool.Stairs:
                return PressStairs(file, hit, point);
            case BuildingTool.Elevator:
                return PressElevator(file, hit, point);
            case BuildingTool.Roof:
                Picked = new PlanPick(PlanPickKind.Roof, _level, 0);
                Changed?.Invoke();
                return true;
            case BuildingTool.Camera:
                return PressCamera(file, storey, hit, point, pixel);
            default:
                return false;
        }
    }

    public void Leave()
    {
        if (_drag != Drag.None)
        {
            return;
        }

        Gizmo.Leave();
        Hover = null;
        Changed?.Invoke();
    }

    public void Move(Vector2 pixel, int width, int height)
    {
        _width = width;
        _height = height;
        if (_file() is not { } file || file.FindStorey(_level) is not { } storey || !TryGetRay(pixel, out Ray ray))
        {
            return;
        }

        if (_drag == Drag.Gizmo)
        {
            DragGizmo(file, storey, pixel);
            return;
        }

        if (_drag == Drag.None)
        {
            bool onGizmo = HoverGizmo(pixel);
            PlanPick? hover = onGizmo ? null : FindHover(file, storey, ray, pixel);
            if (hover != Hover)
            {
                Hover = hover;
                Changed?.Invoke();
            }

            return;
        }

        if (!TryGetPlanePoint(ray, _dragHeight, out Vector2 point))
        {
            return;
        }

        _dragEnd = SnapPoint(point);
        switch (_drag)
        {
            case Drag.Push:
                DragPush(storey, point);
                break;
            case Drag.Arc:
                DragArc(storey, point);
                break;
            case Drag.Corner:
                _dragEnd = SnapPoint(point + _grabOffset);
                DragCorner();
                break;
            case Drag.Along:
                DragAlong(file, storey, ray, point);
                break;
            case Drag.Lift:
                DragLift(file, storey, pixel);
                break;
            case Drag.Handle:
                DragHandle(file, storey, ray);
                break;
            case Drag.Pillar:
                DragPillar();
                break;
            case Drag.Stairs:
            case Drag.Elevator:
                DragShaft();
                break;
            case Drag.StairsHeading:
            case Drag.StairsWidth:
            case Drag.StairsRun:
                DragStairsShape(file, point);
                break;
            case Drag.ElevatorSize:
                DragElevatorSize(file, point);
                break;
            case Drag.Draw:
                _dragEnd = SnapWallEnd(storey, point);
                Changed?.Invoke();
                break;
            default:
                Changed?.Invoke();
                break;
        }
    }

    public void Release()
    {
        Drag drag = _drag;
        _drag = Drag.None;
        _joins = null;
        if (drag == Drag.Gizmo)
        {
            Gizmo.EndDrag();
            Changed?.Invoke();
            return;
        }

        if (_file() is not { } file || file.FindStorey(_level) is not { } storey)
        {
            return;
        }

        int level = _level;
        Vector2 from = _dragStart;
        Vector2 to = _dragEnd;
        if (drag == Drag.Draw)
        {
            if (Vector2.Distance(from, to) >= PlanEdits.Join)
            {
                bool outside = WallOutside;
                Edit("Wall", building => PlanEdits.AddWall(building.FindStorey(level)!, from, to, outside));
            }
            else
            {
                Warn("Drag from where the wall starts to where it ends.");
            }
        }
        else if (drag == Drag.Rectangle)
        {
            Vector2 size = Vector2.Abs(to - from);
            if (size.X >= PlanEdits.Join && size.Y >= PlanEdits.Join)
            {
                string name = NextRoomName(storey);
                Edit("Room", building => PlanEdits.AddRoom(building.FindStorey(level)!, from, to, name));
            }
            else
            {
                Warn("Drag from one corner of the room to the opposite one.");
            }
        }
        else if (drag is Drag.Along or Drag.Lift && Picked is { Kind: PlanPickKind.Opening } pick && storey.Openings.Find(opening => opening.Id == pick.Id) is { } placed)
        {
            if (!OpeningShape.Fits(file, storey, placed))
            {
                Warn($"{placed.Name} doesn't fit there: it reaches past its wall or its floor. It is shown red until it does.");
            }
            else if (OpeningShape.FindOverlapped(storey, placed) is { } other)
            {
                Warn($"{placed.Name} runs into {other.Name} there. Move one of them clear; it is shown red until it is.");
            }
        }

        Changed?.Invoke();
    }

    private void Settle()
    {
        _grouped = false;
        _drag = Drag.None;
        PendingName = string.Empty;
        Pushed = 0;
        _before.Clear();
        _ghost.Clear();
        Gizmo.EndDrag();
    }

    /// <summary>The ray through the pixel, in the building's space.</summary>
    private bool TryGetRay(Vector2 pixel, out Ray ray)
    {
        ray = default;
        if (!Matrix4x4.Invert(_place(), out Matrix4x4 toBuilding))
        {
            return false;
        }

        Ray world = _camera.GetRay(pixel, _width, _height);
        Vector3 origin = Vector3.Transform(world.Origin, toBuilding);
        Vector3 direction = Vector3.TransformNormal(world.Direction, toBuilding);
        ray = new Ray(origin, direction);
        return true;
    }

    /// <summary>Where the ray meets the level plane at a height, across the building's floor.</summary>
    private static bool TryGetPlanePoint(Ray ray, float height, out Vector2 point)
    {
        point = default;
        if (!PlanPicker.TryGetFloorPoint(ray, height, out Vector3 hit, out _))
        {
            return false;
        }

        point = new Vector2(hit.X, hit.Z);
        return true;
    }

    private Vector2 SnapPoint(Vector2 point)
    {
        if (!SnapOn || Snap <= 0)
        {
            return point;
        }

        return new Vector2(MathF.Round(point.X / Snap) * Snap, MathF.Round(point.Y / Snap) * Snap);
    }

    private float SnapLength(float length)
    {
        if (!SnapOn || Snap <= 0)
        {
            return length;
        }

        return MathF.Round(length / Snap) * Snap;
    }

    private float SnapTurn(float degrees)
    {
        if (!SnapOn)
        {
            return degrees;
        }

        return MathF.Round(degrees / TurnStep) * TurnStep;
    }

    /// <summary>
    /// Where a wall being drawn ends: on a corner or a wall near the point, or a wall's middle, when it snaps to them, else on the grid;
    /// what it joins is remembered for its tag.
    /// </summary>
    private Vector2 SnapWallEnd(BuildingStorey storey, Vector2 point)
    {
        _joins = null;
        if (SnapToWalls && PlanEdits.FindCorner(storey, point, Reach) is { } corner)
        {
            _joins = "Joins a corner";
            return PlanGeometry.GetPoint(corner);
        }

        if ((SnapToWalls || SnapToMiddles) && PlanEdits.FindWall(storey, point, Reach) is { } hit)
        {
            // The point goes to the grid first and then straight onto the wall, so a wall drawn along a grid line meets it on that line.
            List<Vector2> path = PlanGeometry.GetPath(storey, hit.Wall);
            float length = PlanGeometry.GetLength(path);
            Vector2 onGrid = SnapPoint(point);
            (float along, _) = PlanGeometry.Project(path, onGrid);
            string name = GetWallName(hit.Wall);
            if (SnapToMiddles && MathF.Abs(along - (length / 2)) <= Reach)
            {
                _joins = $"Joins the middle of {name}";
                return PlanGeometry.GetAlong(path, length / 2).Point;
            }

            if (SnapToWalls)
            {
                _joins = $"Joins {name}";
                return PlanGeometry.GetAlong(path, along).Point;
            }
        }

        return SnapPoint(point);
    }

    private static string GetWallName(PlanWall wall) => wall.Outside ? $"outside wall {wall.Id}" : $"wall {wall.Id}";

    private static string NextRoomName(BuildingStorey storey)
    {
        for (int number = 1; ; number++)
        {
            string name = $"Room {number}";
            if (!storey.Rooms.Exists(room => room.Name == name))
            {
                return name;
            }
        }
    }

    private static string Format(float value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
}
