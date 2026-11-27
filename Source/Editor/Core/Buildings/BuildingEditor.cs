using System.Globalization;
using System.Numerics;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Editor.Core.Buildings;

/// <summary>
/// The building creator's tools in the viewport, on the storey being edited: clicks and drags on its floor's plane push walls, move
/// corners, split and bend walls, draw walls and rooms, stand pillars, place and slide openings, stairs and elevators, and drop camera
/// shots and their triggers. Each edit is held as one undo step that waits, as the confirm bar shows, until it is applied or cancelled;
/// the next edit applies it. Draws the plan and labels its rooms, sizes and drags.
/// </summary>
public sealed class BuildingEditor : ICreatorTool
{
    /// <summary>How near a click must come to a wall, corner or opening to take it, in metres.</summary>
    public const float Reach = 0.45f;

    /// <summary>How far back a dropped shot stands from the point it looks at, in metres.</summary>
    public const float ShotDistance = 14;

    private static readonly Vector4 PlanColor = new(0.85f, 0.78f, 0.66f, 0.55f);
    private static readonly Vector4 HoverColor = new(1, 0.72f, 0.3f, 0.9f);
    private static readonly Vector4 PickColor = new(1, 0.55f, 0.1f, 1);
    private static readonly Vector4 WrongColor = new(0.95f, 0.25f, 0.2f, 1);
    private static readonly Vector4 LiftColor = new(0.45f, 0.85f, 0.35f, 1);
    private static readonly Vector4 TriggerColor = new(1, 0.62f, 0.2f, 0.8f);

    private readonly EditorCamera _camera;
    private readonly Func<Matrix4x4> _place;
    private readonly Func<BuildingFile?> _file;
    private readonly Action<string, Action<BuildingFile>> _change;
    private readonly UndoHistory _history;
    private BuildingTool _tool;
    private int _level;
    private Drag _drag;
    private Vector2 _dragStart;
    private Vector2 _dragEnd;
    private Vector2 _pixelStart;
    private float _dragValue;
    private float _grab;
    private int _width = 1;
    private int _height = 1;
    private Vector2? _pointer;
    private bool _grouped;

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
        Stairs,
        Elevator,
    }

    /// <summary>Raised when the tool, the storey, the pick, the hover or a pending edit changes.</summary>
    public event Action? Changed;

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

    /// <summary>Picks something of the building, as a click in the Parts tab does.</summary>
    public void Pick(PlanPick? pick)
    {
        Picked = pick;
        if (pick is not null && pick.Kind != PlanPickKind.Roof && pick.Kind != PlanPickKind.Stairs && pick.Kind != PlanPickKind.Elevator && pick.Kind != PlanPickKind.Shot)
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

        _grouped = false;
        _drag = Drag.None;
        PendingName = string.Empty;
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

        _grouped = false;
        _drag = Drag.None;
        PendingName = string.Empty;
        _history.CancelGroup();
        Changed?.Invoke();
    }

    /// <summary>Edits the building as one step of the pending edit, which starts with it when none waits.</summary>
    public void Edit(string name, Action<BuildingFile> edit)
    {
        if (!_grouped)
        {
            _history.BeginGroup(name);
            _grouped = true;
        }

        PendingName = name;
        _change(name, edit);
        Changed?.Invoke();
    }

    public bool Press(Vector2 pixel, int width, int height)
    {
        _width = width;
        _height = height;
        if (_file() is not { } file || file.FindStorey(_level) is not { } storey || !TryHit(pixel, file, out Vector2 point))
        {
            return false;
        }

        if (_tool == BuildingTool.Select)
        {
            Picked = FindPick(file, storey, point);
            Changed?.Invoke();
            return false;
        }

        if (_tool is not (BuildingTool.Opening or BuildingTool.Stairs or BuildingTool.Elevator or BuildingTool.Camera))
        {
            Apply();
        }

        _pixelStart = pixel;
        _dragStart = point;
        _dragEnd = point;
        _dragValue = 0;
        switch (_tool)
        {
            case BuildingTool.PushWall:
                return BeginOnWall(storey, point, Drag.Push);
            case BuildingTool.Arc:
                return BeginOnWall(storey, point, Drag.Arc);
            case BuildingTool.MoveCorner:
                return PressCorner(storey, point);
            case BuildingTool.SplitWall:
                return PressSplit(storey, point);
            case BuildingTool.Wall:
                _dragStart = SnapPoint(point);
                _dragEnd = _dragStart;
                _drag = Drag.Draw;
                return true;
            case BuildingTool.Room:
                _dragStart = SnapPoint(point);
                _dragEnd = _dragStart;
                _drag = Drag.Rectangle;
                return true;
            case BuildingTool.Pillar:
                return PressPillar(storey, point);
            case BuildingTool.Opening:
                return PressOpening(file, storey, point, pixel);
            case BuildingTool.Stairs:
                return PressStairs(file, point);
            case BuildingTool.Elevator:
                return PressElevator(file, point);
            case BuildingTool.Roof:
                Picked = new PlanPick(PlanPickKind.Roof, _level, 0);
                Changed?.Invoke();
                return true;
            case BuildingTool.Camera:
                return PressCamera(file, storey, point, pixel);
            default:
                return false;
        }
    }

    public void Move(Vector2 pixel, int width, int height)
    {
        _width = width;
        _height = height;
        _pointer = pixel;
        if (_file() is not { } file || file.FindStorey(_level) is not { } storey || !TryHit(pixel, file, out Vector2 point))
        {
            return;
        }

        if (_drag == Drag.None)
        {
            PlanPick? hover = FindHover(file, storey, point);
            if (hover != Hover)
            {
                Hover = hover;
                Changed?.Invoke();
            }

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
                DragCorner();
                break;
            case Drag.Along:
                DragAlong(storey, point);
                break;
            case Drag.Lift:
                DragLift(file, storey, pixel);
                break;
            case Drag.Stairs:
            case Drag.Elevator:
                DragShaft();
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
        if (_file() is not { } file || file.FindStorey(_level) is null)
        {
            return;
        }

        int level = _level;
        Vector2 from = _dragStart;
        Vector2 to = _dragEnd;
        if (drag == Drag.Draw && Vector2.Distance(from, to) >= PlanEdits.Join)
        {
            bool outside = WallOutside;
            Edit("Wall", building => PlanEdits.AddWall(building.FindStorey(level)!, from, to, outside));
        }
        else if (drag == Drag.Rectangle)
        {
            string name = NextRoomName(file.FindStorey(level)!);
            Edit("Room", building => PlanEdits.AddRoom(building.FindStorey(level)!, from, to, name));
        }

        Changed?.Invoke();
    }

    /// <summary>The labels over the viewport: the storey's rooms with their areas, the picked wall's length, and the drag's tag.</summary>
    public IReadOnlyList<PlanLabel> GetLabels()
    {
        List<PlanLabel> labels = [];
        if (_file() is not { } file || file.FindStorey(_level) is not { } storey)
        {
            return labels;
        }

        Matrix4x4 place = _place();
        float floor = file.GetFloorHeight(_level);
        foreach (RoomShape room in PlanGeometry.FindRooms(storey))
        {
            Vector2 inside = Polygons.InteriorPoint(room.Polygon);
            string name = room.Record >= 0 ? storey.Rooms[room.Record].Name : "Room";
            string detail = $"{Format(room.Area, "0.0")} m²";
            if (_tool == BuildingTool.Camera)
            {
                detail = FindShotOf(file, name) ?? "no shot";
            }

            labels.Add(new PlanLabel(ToWorld(place, inside, floor), name, detail, PlanLabelKind.Room));
        }

        if (Picked is { Kind: PlanPickKind.Wall } wallPick && PlanGeometry.FindWall(storey, wallPick.Id) is { } wall)
        {
            List<Vector2> path = PlanGeometry.GetPath(storey, wall);
            (Vector2 middle, _) = PlanGeometry.GetAlong(path, PlanGeometry.GetLength(path) / 2);
            Vector2 outward = PlanEdits.GetNormal(storey, wall) * (PlanGeometry.GetThickness(storey, wall) + 0.4f);
            string length = $"{Format(PlanGeometry.GetLength(path), "0.00")} m";
            labels.Add(new PlanLabel(ToWorld(place, middle + outward, floor), length, string.Empty, PlanLabelKind.Dimension));
        }

        if (_tool == BuildingTool.Camera)
        {
            foreach (BuildingShot shot in file.Shots)
            {
                string count = shot.Triggers.Count == 1 ? "1 trigger" : $"{shot.Triggers.Count} triggers";
                labels.Add(new PlanLabel(Vector3.Transform(shot.Position, place), shot.Name, count, PlanLabelKind.Shot));
            }
        }

        if (_grouped && !string.IsNullOrEmpty(PendingName) && _pointer is not null)
        {
            Vector2 at = _dragEnd;
            labels.Add(new PlanLabel(ToWorld(place, at, floor), PendingName, string.Empty, PlanLabelKind.Drag));
        }
        else if (_drag is Drag.Draw or Drag.Rectangle)
        {
            Vector2 size = Vector2.Abs(_dragEnd - _dragStart);
            string text = _drag == Drag.Draw ? $"{Format(Vector2.Distance(_dragStart, _dragEnd), "0.00")} m" : $"{Format(size.X, "0.0")} × {Format(size.Y, "0.0")} m";
            labels.Add(new PlanLabel(ToWorld(place, _dragEnd, floor), text, string.Empty, PlanLabelKind.Drag));
        }

        return labels;
    }

    public void Draw(OverlayBuilder overlay)
    {
        if (_file() is not { } file || file.FindStorey(_level) is not { } storey)
        {
            return;
        }

        Matrix4x4 place = _place();
        float floor = file.GetFloorHeight(_level) + 0.02f;
        if (_tool != BuildingTool.Select && _tool != BuildingTool.Roof && _tool != BuildingTool.Camera)
        {
            DrawPlan(overlay, storey, place, floor);
        }

        DrawPick(overlay, file, storey, place, floor, Hover, HoverColor);
        DrawPick(overlay, file, storey, place, floor, Picked, PickColor);
        if (_drag == Drag.Draw)
        {
            overlay.AddLine(ToWorld(place, _dragStart, floor), ToWorld(place, _dragEnd, floor), PickColor, 3);
        }
        else if (_drag == Drag.Rectangle)
        {
            Vector2[] corners = [_dragStart, new(_dragEnd.X, _dragStart.Y), _dragEnd, new(_dragStart.X, _dragEnd.Y)];
            DrawLoop(overlay, corners, place, floor, PickColor, 2);
        }

        if (_tool == BuildingTool.Opening)
        {
            foreach (PlanOpening opening in storey.Openings)
            {
                DrawOpening(overlay, file, storey, opening, place);
            }
        }

        if (_tool == BuildingTool.Camera)
        {
            DrawShots(overlay, file, place);
        }
    }

    private void DrawPlan(OverlayBuilder overlay, BuildingStorey storey, in Matrix4x4 place, float floor)
    {
        foreach (PlanWall wall in storey.Walls)
        {
            List<Vector2> path = PlanGeometry.GetPath(storey, wall);
            DrawPath(overlay, path, place, floor, PlanColor, 1);
        }

        if (_tool is BuildingTool.MoveCorner or BuildingTool.Wall or BuildingTool.Room or BuildingTool.SplitWall)
        {
            foreach (PlanCorner corner in storey.Corners)
            {
                overlay.AddCube(ToWorld(place, PlanGeometry.GetPoint(corner), floor), 4, PlanColor);
            }
        }
    }

    private void DrawPick(OverlayBuilder overlay, BuildingFile file, BuildingStorey storey, in Matrix4x4 place, float floor, PlanPick? pick, Vector4 color)
    {
        if (pick is null)
        {
            return;
        }

        switch (pick.Kind)
        {
            case PlanPickKind.Wall when PlanGeometry.FindWall(storey, pick.Id) is { } wall:
                List<Vector2> path = PlanGeometry.GetPath(storey, wall);
                float top = file.GetFloorHeight(_level) + PlanGeometry.GetHeight(storey, wall);
                DrawPath(overlay, path, place, floor, color, 3);
                DrawPath(overlay, path, place, top, color, 2);
                if (_tool == BuildingTool.PushWall && pick == Picked)
                {
                    (Vector2 middle, _) = PlanGeometry.GetAlong(path, PlanGeometry.GetLength(path) / 2);
                    Vector2 normal = PlanEdits.GetNormal(storey, wall);
                    Vector3 from = ToWorld(place, middle, floor);
                    Vector3 to = ToWorld(place, middle + (normal * 1.2f), floor);
                    overlay.AddLine(from, to, color, 3);
                    overlay.AddCone(to, ToWorld(place, middle + (normal * 1.5f), floor), 7, color);
                }

                break;
            case PlanPickKind.Corner when PlanGeometry.FindCorner(storey, pick.Id) is { } corner:
                overlay.AddCube(ToWorld(place, PlanGeometry.GetPoint(corner), floor), 7, color);
                break;
            case PlanPickKind.Room:
                foreach (RoomShape room in PlanGeometry.FindRooms(storey))
                {
                    if (room.Record >= 0 && storey.Rooms[room.Record].Name == pick.Room)
                    {
                        DrawLoop(overlay, room.Polygon, place, floor, color, 2);
                    }
                }

                break;
            case PlanPickKind.Pillar when storey.Pillars.Find(pillar => pillar.Id == pick.Id) is { } pillar:
                overlay.AddCircle(ToWorld(place, new Vector2(pillar.X, pillar.Z), floor), Vector3.UnitY, (pillar.Size / 2) + 0.05f, color, 2);
                break;
            case PlanPickKind.Stairs when BuildingRegions.GetStairs(file, pick.Id) is { } stairs:
                DrawRegion(overlay, stairs, place, color);
                break;
            case PlanPickKind.Elevator when BuildingRegions.GetElevator(file, pick.Id) is { } shaft:
                DrawRegion(overlay, shaft, place, color);
                break;
            case PlanPickKind.Opening when storey.Openings.Find(opening => opening.Id == pick.Id) is { } opening && _tool != BuildingTool.Opening:
                DrawOpening(overlay, file, storey, opening, place);
                break;
            default:
                break;
        }
    }

    /// <summary>The opening's outline on its wall: picked orange, red where it no longer fits, with its lift handle.</summary>
    private void DrawOpening(OverlayBuilder overlay, BuildingFile file, BuildingStorey storey, PlanOpening opening, in Matrix4x4 place)
    {
        if (opening.Kind == OpeningKind.Floor)
        {
            float floor = file.GetFloorHeight(storey.Level) + 0.02f;
            Vector2 half = new(opening.Width / 2, opening.Height / 2);
            Vector2 middle = new(opening.X, opening.Z);
            Vector2[] corners = [middle - half, new(middle.X + half.X, middle.Y - half.Y), middle + half, new(middle.X - half.X, middle.Y + half.Y)];
            DrawLoop(overlay, corners, place, floor, PickColor, 2);
            return;
        }

        if (!OpeningShape.TryGetFrame(file, storey, opening, out OpeningFrame frame))
        {
            return;
        }

        bool picked = Picked is { Kind: PlanPickKind.Opening } pick && pick.Id == opening.Id && pick.Level == storey.Level;
        Vector4 color = PlanColor;
        if (picked)
        {
            color = PickColor;
        }

        if (!OpeningShape.Fits(file, storey, opening))
        {
            color = WrongColor;
        }

        List<Vector2> profile = OpeningShape.GetProfile(opening, opening.Margin);
        float face = (frame.WallThickness / 2) + 0.01f;
        foreach (float side in new[] { face, -face })
        {
            Vector3 previous = Vector3.Zero;
            for (int index = 0; index <= profile.Count; index++)
            {
                Vector2 point = profile[index % profile.Count];
                Vector3 local = frame.Origin + (frame.Along * point.X) + (Vector3.UnitY * point.Y) + (frame.Out * side);
                Vector3 world = Vector3.Transform(local, place);
                if (index > 0)
                {
                    overlay.AddLine(previous, world, color, picked ? 3 : 1.5f);
                }

                previous = world;
            }
        }

        if (picked)
        {
            Vector3 foot = Vector3.Transform(frame.Origin + (frame.Out * face), place);
            Vector3 handle = Vector3.Transform(frame.Origin + (Vector3.UnitY * 0.6f) + (frame.Out * face), place);
            overlay.AddLine(foot, handle, LiftColor, 3);
            overlay.AddCone(handle, handle + Vector3.TransformNormal(Vector3.UnitY * 0.2f, place), 6, LiftColor);
            overlay.AddCube(foot, 5, LiftColor);
        }
    }

    private void DrawShots(OverlayBuilder overlay, BuildingFile file, in Matrix4x4 place)
    {
        foreach (BuildingShot shot in file.Shots)
        {
            bool picked = Picked is { Kind: PlanPickKind.Shot } pick && pick.Id == shot.Id;
            Vector3 position = Vector3.Transform(shot.Position, place);
            Vector4 color = picked ? PickColor : PlanColor;
            overlay.AddIcon(position, EditorIcon.Camera, color);
            Vector3 forward = Vector3.Transform(-Vector3.UnitZ, shot.Rotation);
            overlay.AddLine(position, position + (Vector3.TransformNormal(forward, place) * 2), color, 2);
            if (!picked)
            {
                continue;
            }

            foreach (ShotTrigger trigger in shot.Triggers)
            {
                if (BuildingRegions.GetTrigger(file, trigger) is { } region)
                {
                    DrawRegion(overlay, region, place, TriggerColor);
                    Vector3 middle = Vector3.Transform(Vector3.Transform(region.Middle, region.Place.ToMatrix()), place);
                    overlay.AddLine(position, middle, TriggerColor, 1);
                }
            }
        }
    }

    private static void DrawRegion(OverlayBuilder overlay, in BuildingRegion region, in Matrix4x4 place, Vector4 color)
    {
        Vector3 half = region.Size / 2;
        Matrix4x4 world = region.Place.ToMatrix() * place;
        overlay.AddBox(region.Middle - half, region.Middle + half, world, color, 2);
    }

    private static void DrawPath(OverlayBuilder overlay, IReadOnlyList<Vector2> path, in Matrix4x4 place, float height, Vector4 color, float width)
    {
        for (int index = 1; index < path.Count; index++)
        {
            overlay.AddLine(ToWorld(place, path[index - 1], height), ToWorld(place, path[index], height), color, width);
        }
    }

    private static void DrawLoop(OverlayBuilder overlay, IReadOnlyList<Vector2> loop, in Matrix4x4 place, float height, Vector4 color, float width)
    {
        for (int index = 0; index < loop.Count; index++)
        {
            Vector2 next = loop[(index + 1) % loop.Count];
            overlay.AddLine(ToWorld(place, loop[index], height), ToWorld(place, next, height), color, width);
        }
    }

    private static Vector3 ToWorld(in Matrix4x4 place, Vector2 point, float height)
    {
        Vector3 local = new(point.X, height, point.Y);
        return Vector3.Transform(local, place);
    }

    private bool BeginOnWall(BuildingStorey storey, Vector2 point, Drag drag)
    {
        if (PlanEdits.FindWall(storey, point, Reach) is not { } hit)
        {
            return false;
        }

        Picked = new PlanPick(PlanPickKind.Wall, _level, hit.Wall.Id);
        _drag = drag;
        _dragValue = 0;
        if (drag == Drag.Arc)
        {
            _dragValue = hit.Wall.Bulge;
        }

        Changed?.Invoke();
        return true;
    }

    private void DragPush(BuildingStorey storey, Vector2 point)
    {
        if (Picked is not { Kind: PlanPickKind.Wall } pick || PlanGeometry.FindWall(storey, pick.Id) is not { } wall)
        {
            return;
        }

        Vector2 normal = PlanEdits.GetNormal(storey, wall);
        float distance = SnapLength(Vector2.Dot(point - _dragStart, normal));
        float step = distance - _dragValue;
        if (MathF.Abs(step) < 1e-4f)
        {
            return;
        }

        _dragValue = distance;
        int level = _level;
        int id = pick.Id;
        bool carry = CarryOpenings;
        string name = $"Push wall · {Format(distance, "+0.00;-0.00")} m";
        Edit(name, building => PlanEdits.PushWall(building.FindStorey(level)!, id, step, carry));
    }

    private void DragArc(BuildingStorey storey, Vector2 point)
    {
        if (Picked is not { Kind: PlanPickKind.Wall } pick || PlanGeometry.FindWall(storey, pick.Id) is not { } wall)
        {
            return;
        }

        Vector2 from = PlanGeometry.GetPoint(PlanGeometry.FindCorner(storey, wall.From)!);
        Vector2 to = PlanGeometry.GetPoint(PlanGeometry.FindCorner(storey, wall.To)!);
        Vector2 middle = (from + to) / 2;
        float bulge = SnapLength(Vector2.Dot(point - middle, Polygons.Left(to - from)));
        float half = Vector2.Distance(from, to) / 2;
        bulge = Math.Clamp(bulge, -half, half);
        if (MathF.Abs(bulge - wall.Bulge) < 1e-4f)
        {
            return;
        }

        int level = _level;
        int id = pick.Id;
        string name = $"Arc · {Format(bulge, "0.00")} m";
        Edit(name, building => PlanEdits.SetBulge(building.FindStorey(level)!, id, bulge));
    }

    private bool PressCorner(BuildingStorey storey, Vector2 point)
    {
        if (PlanEdits.FindCorner(storey, point, Reach) is not { } corner)
        {
            return false;
        }

        Picked = new PlanPick(PlanPickKind.Corner, _level, corner.Id);
        _drag = Drag.Corner;
        Changed?.Invoke();
        return true;
    }

    private void DragCorner()
    {
        if (Picked is not { Kind: PlanPickKind.Corner } pick)
        {
            return;
        }

        int level = _level;
        int id = pick.Id;
        Vector2 point = _dragEnd;
        string name = $"Move corner · {Format(point.X, "0.00")}, {Format(point.Y, "0.00")}";
        Edit(name, building => PlanEdits.MoveCorner(building.FindStorey(level)!, id, point));
    }

    private bool PressSplit(BuildingStorey storey, Vector2 point)
    {
        if (PlanEdits.FindWall(storey, point, Reach) is not { } hit)
        {
            return false;
        }

        int level = _level;
        int id = hit.Wall.Id;
        float along = SnapLength(hit.Along);
        Edit("Split wall", building => PlanEdits.SplitWall(building.FindStorey(level)!, id, along));
        Picked = new PlanPick(PlanPickKind.Wall, _level, id);
        Changed?.Invoke();
        return true;
    }

    private bool PressPillar(BuildingStorey storey, Vector2 point)
    {
        if (storey.Pillars.Find(pillar => Vector2.Distance(new Vector2(pillar.X, pillar.Z), point) <= Math.Max(pillar.Size, Reach)) is { } existing)
        {
            Picked = new PlanPick(PlanPickKind.Pillar, _level, existing.Id);
            Changed?.Invoke();
            return true;
        }

        int level = _level;
        Vector2 at = SnapPoint(point);
        float size = PillarSize;
        bool round = PillarRound;
        int id = storey.NextId();
        Edit("Pillar", building => PlanEdits.AddPillar(building.FindStorey(level)!, at, size, round));
        Picked = new PlanPick(PlanPickKind.Pillar, _level, id);
        Changed?.Invoke();
        return true;
    }

    private bool PressOpening(BuildingFile file, BuildingStorey storey, Vector2 point, Vector2 pixel)
    {
        if (Picked is { Kind: PlanPickKind.Opening } picked && storey.Openings.Find(opening => opening.Id == picked.Id) is { Kind: not OpeningKind.Floor } current
            && IsNearLift(file, storey, current, pixel))
        {
            _drag = Drag.Lift;
            _dragValue = current.Lift;
            return true;
        }

        if (FindOpening(storey, point) is { } existing)
        {
            if (Picked?.Id != existing.Id)
            {
                Apply();
            }

            Picked = new PlanPick(PlanPickKind.Opening, _level, existing.Id);
            _drag = Drag.Along;
            _grab = 0;
            if (existing.Kind != OpeningKind.Floor && PlanGeometry.FindWall(storey, existing.Wall) is { } wall)
            {
                (float along, _) = PlanGeometry.Project(PlanGeometry.GetPath(storey, wall), point);
                _grab = along - existing.Along;
            }

            Changed?.Invoke();
            return true;
        }

        Apply();
        int level = _level;
        int id = storey.NextId();
        OpeningKind kind = NewOpening;
        if (kind == OpeningKind.Floor)
        {
            Vector2 at = SnapPoint(point);
            Edit("Hole", building => PlanEdits.AddHole(building.FindStorey(level)!, at, 1.2f, 1.2f));
        }
        else
        {
            if (PlanEdits.FindWall(storey, point, Reach) is not { } hit)
            {
                return false;
            }

            int wallId = hit.Wall.Id;
            float along = SnapLength(hit.Along);
            Edit(kind.ToString(), building => PlanEdits.AddOpening(building.FindStorey(level)!, wallId, along, kind));
        }

        Picked = new PlanPick(PlanPickKind.Opening, level, id);
        _drag = Drag.Along;
        _grab = 0;
        Changed?.Invoke();
        return true;
    }

    private bool IsNearLift(BuildingFile file, BuildingStorey storey, PlanOpening opening, Vector2 pixel)
    {
        if (!OpeningShape.TryGetFrame(file, storey, opening, out OpeningFrame frame))
        {
            return false;
        }

        Matrix4x4 place = _place();
        Vector3 handle = Vector3.Transform(frame.Origin + (Vector3.UnitY * 0.6f) + (frame.Out * ((frame.WallThickness / 2) + 0.01f)), place);
        Vector2 shown = _camera.Project(handle, _width, _height);
        return Vector2.Distance(shown, pixel) <= 14;
    }

    /// <summary>The opening under the point: a hole that holds it, or one on a wall within reach whose width covers it.</summary>
    private static PlanOpening? FindOpening(BuildingStorey storey, Vector2 point)
    {
        foreach (PlanOpening opening in storey.Openings)
        {
            if (opening.Kind == OpeningKind.Floor)
            {
                Vector2 offset = Vector2.Abs(point - new Vector2(opening.X, opening.Z));
                if (offset.X <= opening.Width / 2 && offset.Y <= opening.Height / 2)
                {
                    return opening;
                }

                continue;
            }

            if (PlanGeometry.FindWall(storey, opening.Wall) is not { } wall)
            {
                continue;
            }

            (float along, float distance) = PlanGeometry.Project(PlanGeometry.GetPath(storey, wall), point);
            if (distance <= Reach && MathF.Abs(along - opening.Along) <= opening.Width / 2)
            {
                return opening;
            }
        }

        return null;
    }

    private void DragAlong(BuildingStorey storey, Vector2 point)
    {
        if (Picked is not { Kind: PlanPickKind.Opening } pick || storey.Openings.Find(opening => opening.Id == pick.Id) is not { } opening)
        {
            return;
        }

        int level = _level;
        int id = pick.Id;
        if (opening.Kind == OpeningKind.Floor)
        {
            Vector2 at = SnapPoint(point);
            Edit($"{opening.Name} · moved", building => MoveHole(building, level, id, at));
            return;
        }

        if (PlanEdits.FindWall(storey, point, Reach * 2) is { } hit && hit.Wall.Id != opening.Wall)
        {
            int wallId = hit.Wall.Id;
            float onOther = SnapLength(hit.Along);
            Edit($"{opening.Name} · on another wall", building => MoveOpening(building, level, id, wallId, onOther));
            return;
        }

        if (PlanGeometry.FindWall(storey, opening.Wall) is not { } wall)
        {
            return;
        }

        (float along, _) = PlanGeometry.Project(PlanGeometry.GetPath(storey, wall), point);
        float target = SnapLength(along - _grab);
        if (MathF.Abs(target - opening.Along) < 1e-4f)
        {
            return;
        }

        int wallOf = opening.Wall;
        Edit($"{opening.Name} · {Format(target, "0.00")} m along", building => MoveOpening(building, level, id, wallOf, target));
    }

    private void DragLift(BuildingFile file, BuildingStorey storey, Vector2 pixel)
    {
        if (Picked is not { Kind: PlanPickKind.Opening } pick || storey.Openings.Find(opening => opening.Id == pick.Id) is not { } opening)
        {
            return;
        }

        Vector3 at = Vector3.Transform(new Vector3(_dragStart.X, file.GetFloorHeight(_level), _dragStart.Y), _place());
        float perPixel = _camera.WorldUnitsPerPixel(at, _height);
        float lift = SnapLength(Math.Max(_dragValue + ((_pixelStart.Y - pixel.Y) * perPixel), 0));
        if (MathF.Abs(lift - opening.Lift) < 1e-4f)
        {
            return;
        }

        int level = _level;
        int id = pick.Id;
        Edit($"{opening.Name} · lift {Format(lift, "0.00")} m", building => building.FindStorey(level)!.Openings.Find(found => found.Id == id)!.Lift = lift);
    }

    private static void MoveOpening(BuildingFile building, int level, int id, int wall, float along)
    {
        BuildingStorey storey = building.FindStorey(level)!;
        PlanOpening opening = storey.Openings.Find(found => found.Id == id)!;
        opening.Wall = wall;
        opening.Along = along;
        PlanEdits.KeepOpenings(storey);
    }

    private static void MoveHole(BuildingFile building, int level, int id, Vector2 point)
    {
        PlanOpening hole = building.FindStorey(level)!.Openings.Find(found => found.Id == id)!;
        hole.X = point.X;
        hole.Z = point.Y;
    }

    private bool PressStairs(BuildingFile file, Vector2 point)
    {
        foreach (BuildingStairs stairs in file.Stairs.Where(stairs => stairs.Level == _level))
        {
            if (Contains(BuildingRegions.GetStairs(file, stairs.Id), point))
            {
                if (Picked?.Id != stairs.Id || Picked.Kind != PlanPickKind.Stairs)
                {
                    Apply();
                }

                Picked = new PlanPick(PlanPickKind.Stairs, _level, stairs.Id);
                _drag = Drag.Stairs;
                _grab = 0;
                _dragStart = SnapPoint(point) - new Vector2(stairs.X, stairs.Z);
                Changed?.Invoke();
                return true;
            }
        }

        Apply();
        int level = _level;
        Vector2 foot = SnapPoint(point);
        float heading = StairsHeading;
        StairsKind kind = NewStairs;
        if (file.FindStorey(level + 1) is null)
        {
            return false;
        }

        int id = 1 + file.Stairs.Select(static stairs => stairs.Id).DefaultIfEmpty(0).Max();
        Edit("Stairs", building => BuildingTools.AddStairs(building, level, foot, heading, kind));
        Picked = new PlanPick(PlanPickKind.Stairs, level, id);
        _drag = Drag.Stairs;
        _dragStart = Vector2.Zero;
        Changed?.Invoke();
        return true;
    }

    private bool PressElevator(BuildingFile file, Vector2 point)
    {
        foreach (BuildingElevator elevator in file.Elevators)
        {
            Vector2 offset = Vector2.Abs(point - new Vector2(elevator.X, elevator.Z));
            if (offset.X <= elevator.Size / 2 && offset.Y <= elevator.Size / 2)
            {
                if (Picked?.Id != elevator.Id || Picked.Kind != PlanPickKind.Elevator)
                {
                    Apply();
                }

                Picked = new PlanPick(PlanPickKind.Elevator, _level, elevator.Id);
                _drag = Drag.Elevator;
                _dragStart = SnapPoint(point) - new Vector2(elevator.X, elevator.Z);
                Changed?.Invoke();
                return true;
            }
        }

        Apply();
        Vector2 place = SnapPoint(point);
        int id = 1 + file.Elevators.Select(static elevator => elevator.Id).DefaultIfEmpty(0).Max();
        Edit("Elevator", building => BuildingTools.AddElevator(building, place, 0));
        Picked = new PlanPick(PlanPickKind.Elevator, _level, id);
        _drag = Drag.Elevator;
        _dragStart = Vector2.Zero;
        Changed?.Invoke();
        return true;
    }

    private void DragShaft()
    {
        if (Picked is not { } pick)
        {
            return;
        }

        Vector2 target = _dragEnd - _dragStart;
        int id = pick.Id;
        if (_drag == Drag.Stairs)
        {
            Edit($"Stairs · {Format(target.X, "0.00")}, {Format(target.Y, "0.00")}", building => MoveStairs(building, id, target));
        }
        else
        {
            Edit($"Elevator · {Format(target.X, "0.00")}, {Format(target.Y, "0.00")}", building => MoveElevator(building, id, target));
        }
    }

    private static void MoveStairs(BuildingFile building, int id, Vector2 point)
    {
        BuildingStairs stairs = building.Stairs.Find(found => found.Id == id)!;
        stairs.X = point.X;
        stairs.Z = point.Y;
    }

    private static void MoveElevator(BuildingFile building, int id, Vector2 point)
    {
        BuildingElevator elevator = building.Elevators.Find(found => found.Id == id)!;
        elevator.X = point.X;
        elevator.Z = point.Y;
    }

    private bool PressCamera(BuildingFile file, BuildingStorey storey, Vector2 point, Vector2 pixel)
    {
        if (AddingTrigger && Picked is { Kind: PlanPickKind.Shot } shotPick)
        {
            ShotTrigger trigger = FindTrigger(file, storey, point);
            int id = shotPick.Id;
            Edit("Trigger", building => building.Shots.Find(shot => shot.Id == id)?.Triggers.Add(trigger));
            Apply();
            AddingTrigger = false;
            Changed?.Invoke();
            return true;
        }

        Matrix4x4 place = _place();
        foreach (BuildingShot shot in file.Shots)
        {
            Vector2 shown = _camera.Project(Vector3.Transform(shot.Position, place), _width, _height);
            if (Vector2.Distance(shown, pixel) <= 16)
            {
                Apply();
                Picked = new PlanPick(PlanPickKind.Shot, shot.Level, shot.Id);
                Changed?.Invoke();
                return true;
            }
        }

        DropShot(point);
        return true;
    }

    /// <summary>
    /// Drops a shot on the storey being edited looking down at the point from the view's side, named after the room it looks at and
    /// switched to on entering that room; it is picked.
    /// </summary>
    public void DropShot(Vector2 point)
    {
        if (_file() is not { } file || file.FindStorey(_level) is not { } storey)
        {
            return;
        }

        Apply();
        int level = _level;
        float floor = file.GetFloorHeight(level);
        Vector3 target = new(point.X, floor, point.Y);
        float yaw = _camera.Yaw;
        float pitch = Math.Min(_camera.Pitch, -0.35f);
        (Vector3 position, Quaternion rotation) = BuildingTools.LookAt(target, yaw, pitch, ShotDistance);
        int added = 1 + file.Shots.Select(static shot => shot.Id).DefaultIfEmpty(0).Max();
        string roomName = FindRoomName(storey, point);
        Edit("Shot", building =>
        {
            int shotId = BuildingTools.AddShot(building, level, position, rotation);
            BuildingShot shot = building.Shots.Find(found => found.Id == shotId)!;
            if (!string.IsNullOrEmpty(roomName))
            {
                shot.Name = $"{roomName} shot";
                shot.Triggers.Add(new ShotTrigger { Target = TriggerTarget.Room, Level = level, Room = roomName });
            }
        });
        Apply();
        Picked = new PlanPick(PlanPickKind.Shot, level, added);
        Changed?.Invoke();
    }

    /// <summary>What a click at the point sets off: an opening near it, stairs or an elevator over it, else the room it is in.</summary>
    private ShotTrigger FindTrigger(BuildingFile file, BuildingStorey storey, Vector2 point)
    {
        if (FindOpening(storey, point) is { } opening)
        {
            return new ShotTrigger { Target = TriggerTarget.Opening, Level = _level, Id = opening.Id, When = TriggerMoment.Both, Change = TriggerChange.Cut };
        }

        foreach (BuildingStairs stairs in file.Stairs)
        {
            if (stairs.Level == _level && Contains(BuildingRegions.GetStairs(file, stairs.Id), point))
            {
                return new ShotTrigger { Target = TriggerTarget.Stairs, Level = _level, Id = stairs.Id };
            }
        }

        foreach (BuildingElevator elevator in file.Elevators)
        {
            Vector2 offset = Vector2.Abs(point - new Vector2(elevator.X, elevator.Z));
            if (offset.X <= elevator.Size / 2 && offset.Y <= elevator.Size / 2)
            {
                return new ShotTrigger { Target = TriggerTarget.Elevator, Level = _level, Id = elevator.Id };
            }
        }

        return new ShotTrigger { Target = TriggerTarget.Room, Level = _level, Room = FindRoomName(storey, point) };
    }

    private static string FindRoomName(BuildingStorey storey, Vector2 point)
    {
        foreach (RoomShape room in PlanGeometry.FindRooms(storey))
        {
            if (room.Record >= 0 && Polygons.Contains(room.Polygon, point))
            {
                return storey.Rooms[room.Record].Name;
            }
        }

        return string.Empty;
    }

    private static string? FindShotOf(BuildingFile file, string room)
    {
        foreach (BuildingShot shot in file.Shots)
        {
            if (shot.Triggers.Exists(trigger => trigger.Target == TriggerTarget.Room && trigger.Room == room))
            {
                return shot.Name;
            }
        }

        return null;
    }

    /// <summary>What the click at the point picks with the select tool: an opening, a corner, a wall, a pillar, stairs, an elevator, a room.</summary>
    private PlanPick? FindPick(BuildingFile file, BuildingStorey storey, Vector2 point)
    {
        if (FindOpening(storey, point) is { } opening)
        {
            return new PlanPick(PlanPickKind.Opening, _level, opening.Id);
        }

        if (PlanEdits.FindCorner(storey, point, Reach / 2) is { } corner)
        {
            return new PlanPick(PlanPickKind.Corner, _level, corner.Id);
        }

        if (PlanEdits.FindWall(storey, point, Reach / 2) is { } hit)
        {
            return new PlanPick(PlanPickKind.Wall, _level, hit.Wall.Id);
        }

        if (storey.Pillars.Find(pillar => Vector2.Distance(new Vector2(pillar.X, pillar.Z), point) <= pillar.Size) is { } pillar)
        {
            return new PlanPick(PlanPickKind.Pillar, _level, pillar.Id);
        }

        foreach (BuildingStairs stairs in file.Stairs)
        {
            if (stairs.Level == _level && Contains(BuildingRegions.GetStairs(file, stairs.Id), point))
            {
                return new PlanPick(PlanPickKind.Stairs, _level, stairs.Id);
            }
        }

        foreach (BuildingElevator elevator in file.Elevators)
        {
            Vector2 offset = Vector2.Abs(point - new Vector2(elevator.X, elevator.Z));
            if (offset.X <= elevator.Size / 2 && offset.Y <= elevator.Size / 2)
            {
                return new PlanPick(PlanPickKind.Elevator, _level, elevator.Id);
            }
        }

        string room = FindRoomName(storey, point);
        if (!string.IsNullOrEmpty(room))
        {
            return new PlanPick(PlanPickKind.Room, _level, 0, room);
        }

        return null;
    }

    private PlanPick? FindHover(BuildingFile file, BuildingStorey storey, Vector2 point)
    {
        switch (_tool)
        {
            case BuildingTool.PushWall:
            case BuildingTool.Arc:
            case BuildingTool.SplitWall:
                if (PlanEdits.FindWall(storey, point, Reach) is { } hit)
                {
                    return new PlanPick(PlanPickKind.Wall, _level, hit.Wall.Id);
                }

                return null;
            case BuildingTool.MoveCorner:
            case BuildingTool.Wall:
                if (PlanEdits.FindCorner(storey, point, Reach) is { } corner)
                {
                    return new PlanPick(PlanPickKind.Corner, _level, corner.Id);
                }

                return null;
            case BuildingTool.Opening:
                if (FindOpening(storey, point) is { } opening)
                {
                    return new PlanPick(PlanPickKind.Opening, _level, opening.Id);
                }

                if (PlanEdits.FindWall(storey, point, Reach) is { } wall)
                {
                    return new PlanPick(PlanPickKind.Wall, _level, wall.Wall.Id);
                }

                return null;
            case BuildingTool.Select:
            case BuildingTool.Camera:
                return FindPick(file, storey, point);
            default:
                return null;
        }
    }

    private static bool Contains(BuildingRegion? region, Vector2 point)
    {
        if (region is not { } box)
        {
            return false;
        }

        Matrix4x4 toBox = Matrix4x4.Identity;
        if (Matrix4x4.Invert(box.Place.ToMatrix(), out Matrix4x4 inverse))
        {
            toBox = inverse;
        }

        Vector3 local = Vector3.Transform(new Vector3(point.X, box.Place.Position.Y, point.Y), toBox) - box.Middle;
        return MathF.Abs(local.X) <= box.Size.X / 2 && MathF.Abs(local.Z) <= box.Size.Z / 2;
    }

    /// <summary>Where the ray from the pixel meets the plane of the storey's floor, across the building's floor.</summary>
    private bool TryHit(Vector2 pixel, BuildingFile file, out Vector2 point)
    {
        point = default;
        Ray ray = _camera.GetRay(pixel, _width, _height);
        if (!Matrix4x4.Invert(_place(), out Matrix4x4 toBuilding))
        {
            return false;
        }

        Vector3 origin = Vector3.Transform(ray.Origin, toBuilding);
        Vector3 direction = Vector3.TransformNormal(ray.Direction, toBuilding);
        float floor = file.GetFloorHeight(_level);
        if (MathF.Abs(direction.Y) < 1e-5f)
        {
            return false;
        }

        float distance = (floor - origin.Y) / direction.Y;
        if (distance < 0)
        {
            return false;
        }

        Vector3 hit = origin + (direction * distance);
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
