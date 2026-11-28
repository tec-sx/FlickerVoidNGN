using System.Numerics;

using FlickerVoid.Editor.Core.Gizmos;
using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Buildings;

public sealed partial class BuildingEditor
{
    private const float MinPillarSize = 0.1f;
    private const float MinOpeningSize = 0.2f;
    private const float MinShotSize = 0.5f;

    private GizmoStart? _gizmoStart;

    /// <summary>
    /// The gizmo of the plan: with the select tool and its mode on move, rotate or scale it stands on what is picked, along its own
    /// axes, and moves walls, corners, pillars, openings, door handles, stairs, elevators and shots, turns stairs, elevators and shots,
    /// and sizes pillars, openings, stairs, elevators and shots.
    /// </summary>
    public TransformGizmo Gizmo { get; } = new() { Mode = GizmoMode.Select, Space = GizmoSpace.Local };

    /// <summary>The gizmo's mode; move, rotate and scale take the select tool, as only one of the rail's tools is in hand.</summary>
    public GizmoMode GizmoMode
    {
        get => Gizmo.Mode;
        set
        {
            if (Gizmo.Mode == value)
            {
                return;
            }

            if (value != GizmoMode.Select)
            {
                Tool = BuildingTool.Select;
            }

            Gizmo.Mode = value;
            Changed?.Invoke();
        }
    }

    /// <summary>What a gizmo drag starts from: the gizmo's place in the world and in the building, and the picked thing as it was.</summary>
    private sealed record GizmoStart(Transform World, Transform Local, PlanOpening? Opening, OpeningFrame Frame, BuildingStairs? Stairs, BuildingElevator? Elevator, Vector3 Values, Quaternion Rotation);

    /// <summary>Whether the gizmo's mode does anything to a kind of thing.</summary>
    private static bool GizmoApplies(PlanPickKind kind, GizmoMode mode)
    {
        switch (mode)
        {
            case GizmoMode.Move:
                return kind is PlanPickKind.Wall or PlanPickKind.Corner or PlanPickKind.Pillar or PlanPickKind.Opening or PlanPickKind.Handle
                    or PlanPickKind.Stairs or PlanPickKind.Elevator or PlanPickKind.Shot;
            case GizmoMode.Rotate:
                return kind is PlanPickKind.Stairs or PlanPickKind.Elevator or PlanPickKind.Shot;
            case GizmoMode.Scale:
                return kind is PlanPickKind.Pillar or PlanPickKind.Opening or PlanPickKind.Stairs or PlanPickKind.Elevator or PlanPickKind.Shot;
            default:
                return false;
        }
    }

    /// <summary>Where the gizmo stands on what is picked, in the world; false with nothing picked the mode applies to.</summary>
    public bool TryGetGizmoTarget(out Transform target)
    {
        target = default;
        if (_tool != BuildingTool.Select || Picked is not { } pick || !GizmoApplies(pick.Kind, Gizmo.Mode)
            || _file() is not { } file || file.FindStorey(_level) is not { } storey || !TryGetGizmoPlace(file, storey, pick, out Transform local))
        {
            return false;
        }

        target = ToWorld(local);
        return true;
    }

    private Transform ToWorld(in Transform local)
    {
        Matrix4x4 place = _place();
        Quaternion turn = Quaternion.CreateFromRotationMatrix(place);
        Quaternion rotation = Quaternion.Normalize(turn * local.Rotation);
        return new Transform { Position = Vector3.Transform(local.Position, place), Rotation = rotation, Scale = Vector3.One };
    }

    /// <summary>The gizmo's place on a picked thing, in the building's space, turned along the thing.</summary>
    private bool TryGetGizmoPlace(BuildingFile file, BuildingStorey storey, PlanPick pick, out Transform local)
    {
        local = new Transform { Rotation = Quaternion.Identity, Scale = Vector3.One };
        float floor = file.GetFloorHeight(_level);
        switch (pick.Kind)
        {
            case PlanPickKind.Wall when PlanGeometry.FindWall(storey, pick.Id) is { } wall:
                List<Vector2> path = PlanGeometry.GetPath(storey, wall);
                if (path.Count < 2)
                {
                    return false;
                }

                (Vector2 middle, Vector2 direction) = PlanGeometry.GetAlong(path, PlanGeometry.GetLength(path) / 2);
                float half = PlanGeometry.GetHeight(storey, wall) / 2;
                local = local with { Position = new Vector3(middle.X, floor + half, middle.Y), Rotation = TurnAlong(direction) };
                return true;
            case PlanPickKind.Corner when PlanGeometry.FindCorner(storey, pick.Id) is { } corner:
                local = local with { Position = new Vector3(corner.X, floor, corner.Z) };
                return true;
            case PlanPickKind.Pillar when storey.Pillars.Find(found => found.Id == pick.Id) is { } pillar:
                local = local with { Position = new Vector3(pillar.X, floor + ((storey.Height - storey.Floor) / 2), pillar.Z) };
                return true;
            case PlanPickKind.Opening or PlanPickKind.Handle when storey.Openings.Find(found => found.Id == pick.Id) is { } opening:
                return TryGetOpeningPlace(file, storey, opening, pick.Kind == PlanPickKind.Handle, ref local);
            case PlanPickKind.Stairs when file.Stairs.Find(found => found.Id == pick.Id) is { } stairs:
                local = BuildingMaker.GetStairsPlace(file, stairs) with { Scale = Vector3.One };
                return true;
            case PlanPickKind.Elevator when file.Elevators.Find(found => found.Id == pick.Id) is { } elevator:
                local = BuildingMaker.GetElevatorPlace(file, elevator, _level) with { Scale = Vector3.One };
                return true;
            case PlanPickKind.Shot when file.Shots.Find(found => found.Id == pick.Id) is { } shot:
                local = local with { Position = shot.Position, Rotation = shot.Rotation };
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetOpeningPlace(BuildingFile file, BuildingStorey storey, PlanOpening opening, bool handle, ref Transform local)
    {
        if (opening.Kind == OpeningKind.Floor)
        {
            local = local with { Position = new Vector3(opening.X, file.GetFloorHeight(storey.Level), opening.Z) };
            return !handle;
        }

        if (!OpeningShape.TryGetFrame(file, storey, opening, out OpeningFrame frame))
        {
            return false;
        }

        Vector2 along = new(frame.Along.X, frame.Along.Z);
        Vector3 position = frame.Origin + (Vector3.UnitY * (opening.Height / 2));
        if (handle)
        {
            List<Vector3> handles = GetDoorHandles(frame, opening);
            if (handles.Count == 0)
            {
                return false;
            }

            position = handles[0];
        }

        local = local with { Position = position, Rotation = TurnAlong(along) };
        return true;
    }

    /// <summary>The turn that lays the gizmo's X along a direction across the floor.</summary>
    private static Quaternion TurnAlong(Vector2 direction)
    {
        float turn = MathF.Atan2(-direction.Y, direction.X);
        return Quaternion.CreateFromAxisAngle(Vector3.UnitY, turn);
    }

    private void HoverGizmo(Vector2 pixel)
    {
        if (!TryGetGizmoTarget(out Transform target))
        {
            return;
        }

        GizmoAxis before = Gizmo.Hovered;
        if (Gizmo.Hover(_camera, target, pixel, _width, _height) != before)
        {
            Changed?.Invoke();
        }
    }

    private bool PressGizmo(BuildingFile file, BuildingStorey storey, Vector2 pixel)
    {
        if (Picked is not { } pick || !TryGetGizmoTarget(out Transform target) || !TryGetGizmoPlace(file, storey, pick, out Transform local)
            || !Gizmo.BeginDrag(_camera, target, pixel, _width, _height))
        {
            return false;
        }

        HoldGhost(file, storey, pick);
        _gizmoStart = Snapshot(file, storey, pick, target, local);
        _drag = Drag.Gizmo;
        _dragValue = 0;
        return true;
    }

    private static GizmoStart Snapshot(BuildingFile file, BuildingStorey storey, PlanPick pick, Transform world, Transform local)
    {
        GizmoStart start = new(world, local, null, default, null, null, Vector3.Zero, Quaternion.Identity);
        switch (pick.Kind)
        {
            case PlanPickKind.Wall when PlanGeometry.FindWall(storey, pick.Id) is { } wall:
                Vector2 normal = PlanEdits.GetNormal(storey, wall);
                return start with { Values = new Vector3(normal.X, 0, normal.Y) };
            case PlanPickKind.Corner when PlanGeometry.FindCorner(storey, pick.Id) is { } corner:
                return start with { Values = new Vector3(corner.X, 0, corner.Z) };
            case PlanPickKind.Pillar when storey.Pillars.Find(found => found.Id == pick.Id) is { } pillar:
                return start with { Values = new Vector3(pillar.X, pillar.Size, pillar.Z) };
            case PlanPickKind.Opening or PlanPickKind.Handle when storey.Openings.Find(found => found.Id == pick.Id) is { } opening:
                OpeningShape.TryGetFrame(file, storey, opening, out OpeningFrame frame);
                return start with { Opening = opening.Copy(), Frame = frame };
            case PlanPickKind.Stairs when file.Stairs.Find(found => found.Id == pick.Id) is { } stairs:
                return start with { Stairs = stairs.Copy() };
            case PlanPickKind.Elevator when file.Elevators.Find(found => found.Id == pick.Id) is { } elevator:
                return start with { Elevator = elevator.Copy() };
            case PlanPickKind.Shot when file.Shots.Find(found => found.Id == pick.Id) is { } shot:
                return start with { Values = new Vector3(shot.Size, 0, 0), Rotation = shot.Rotation };
            default:
                return start;
        }
    }

    private void DragGizmo(BuildingFile file, BuildingStorey storey, Vector2 pixel)
    {
        if (_gizmoStart is not { } start || Picked is not { } pick || !Matrix4x4.Invert(_place(), out Matrix4x4 toBuilding))
        {
            return;
        }

        Transform dragged = Gizmo.Drag(_camera, pixel, _width, _height);
        switch (Gizmo.Mode)
        {
            case GizmoMode.Move:
                Vector3 moved = Vector3.TransformNormal(dragged.Position - start.World.Position, toBuilding);
                MoveWithGizmo(storey, pick, start, moved);
                break;
            case GizmoMode.Rotate:
                Quaternion turn = Quaternion.CreateFromRotationMatrix(_place());
                Quaternion world = dragged.Rotation * Quaternion.Inverse(start.World.Rotation);
                Quaternion inBuilding = Quaternion.Normalize(Quaternion.Inverse(turn) * world * turn);
                TurnWithGizmo(pick, start, inBuilding);
                break;
            case GizmoMode.Scale:
                SizeWithGizmo(pick, start, dragged.Scale);
                break;
            default:
                break;
        }
    }

    private void MoveWithGizmo(BuildingStorey storey, PlanPick pick, GizmoStart start, Vector3 moved)
    {
        int level = _level;
        int id = pick.Id;
        Vector2 across = new(moved.X, moved.Z);
        switch (pick.Kind)
        {
            case PlanPickKind.Wall:
                float pushed = SnapLength(Vector3.Dot(moved, start.Values));
                float step = pushed - _dragValue;
                if (MathF.Abs(step) >= 1e-4f)
                {
                    _dragValue = pushed;
                    Push(level, id, step);
                }

                break;
            case PlanPickKind.Corner:
                Vector2 corner = SnapPoint(new Vector2(start.Values.X, start.Values.Z) + across);
                Edit($"Move corner · {Format(corner.X, "0.00")}, {Format(corner.Y, "0.00")}", building => PlanEdits.MoveCorner(building.FindStorey(level)!, id, corner));
                break;
            case PlanPickKind.Pillar:
                Vector2 pillar = SnapPoint(new Vector2(start.Values.X, start.Values.Z) + across);
                Edit($"Pillar · {Format(pillar.X, "0.00")}, {Format(pillar.Y, "0.00")}", building => MovePillar(building, level, id, pillar));
                break;
            case PlanPickKind.Opening when start.Opening is { Kind: OpeningKind.Floor } hole:
                Vector2 at = SnapPoint(new Vector2(hole.X, hole.Z) + across);
                Edit($"{hole.Name} · moved", building => MoveHole(building, level, id, at));
                break;
            case PlanPickKind.Opening when start.Opening is { } opening:
                float along = SnapLength(opening.Along + Vector3.Dot(moved, start.Frame.Along));
                float lift = SnapLength(Math.Max(opening.Lift + moved.Y, 0));
                int wall = opening.Wall;
                Edit($"{opening.Name} · {Format(along, "0.00")} m along, lift {Format(lift, "0.00")} m", building =>
                {
                    MoveOpening(building, level, id, wall, along);
                    building.FindStorey(level)!.Openings.Find(found => found.Id == id)!.Lift = lift;
                });
                break;
            case PlanPickKind.Handle when start.Opening is not null && storey.Openings.Find(found => found.Id == id) is { } current:
                Vector3 from = start.Local.Position + moved - start.Frame.Origin;
                SetHandle(current, start.Frame, Vector3.Dot(from, start.Frame.Along), from.Y);
                break;
            case PlanPickKind.Stairs when start.Stairs is { } stairs:
                Vector2 foot = SnapPoint(new Vector2(stairs.X, stairs.Z) + across);
                Edit($"Stairs · {Format(foot.X, "0.00")}, {Format(foot.Y, "0.00")}", building => MoveStairs(building, id, foot));
                break;
            case PlanPickKind.Elevator when start.Elevator is { } elevator:
                Vector2 shaft = SnapPoint(new Vector2(elevator.X, elevator.Z) + across);
                Edit($"Elevator · {Format(shaft.X, "0.00")}, {Format(shaft.Y, "0.00")}", building => MoveElevator(building, id, shaft));
                break;
            case PlanPickKind.Shot:
                Vector3 position = start.Local.Position + moved;
                Edit("Move shot", building => building.Shots.Find(found => found.Id == id)!.Position = position);
                break;
            default:
                break;
        }
    }

    private void TurnWithGizmo(PlanPick pick, GizmoStart start, Quaternion turned)
    {
        int id = pick.Id;
        Vector3 ahead = Vector3.Transform(Vector3.UnitZ, turned);
        float degrees = float.RadiansToDegrees(MathF.Atan2(ahead.X, ahead.Z));
        switch (pick.Kind)
        {
            case PlanPickKind.Stairs when start.Stairs is { } stairs:
                float climbing = SnapTurn(stairs.Heading + degrees);
                StairsHeading = climbing;
                Edit($"Stairs · climbing {Format(climbing, "0")}°", building => building.Stairs.Find(found => found.Id == id)!.Heading = climbing);
                break;
            case PlanPickKind.Elevator when start.Elevator is { } elevator:
                float facing = SnapTurn(elevator.Heading - degrees);
                Edit($"Elevator · turned {Format(facing, "0")}°", building => building.Elevators.Find(found => found.Id == id)!.Heading = facing);
                break;
            case PlanPickKind.Shot:
                Quaternion rotation = Quaternion.Normalize(turned * start.Rotation);
                Edit("Turn shot", building => building.Shots.Find(found => found.Id == id)!.Rotation = rotation);
                break;
            default:
                break;
        }
    }

    private void SizeWithGizmo(PlanPick pick, GizmoStart start, Vector3 scale)
    {
        int level = _level;
        int id = pick.Id;
        float most = GetMostChanged(scale);
        switch (pick.Kind)
        {
            case PlanPickKind.Pillar:
                float size = Math.Max(SnapLength(start.Values.Y * most), MinPillarSize);
                Edit($"Pillar · {Format(size, "0.00")} m", building => building.FindStorey(level)!.Pillars.Find(found => found.Id == id)!.Size = size);
                break;
            case PlanPickKind.Opening when start.Opening is { } opening:
                float second = opening.Kind == OpeningKind.Floor ? scale.Z : scale.Y;
                float width = Math.Max(SnapLength(opening.Width * scale.X), MinOpeningSize);
                float height = Math.Max(SnapLength(opening.Height * second), MinOpeningSize);
                Edit($"{opening.Name} · {Format(width, "0.00")} × {Format(height, "0.00")} m", building =>
                {
                    BuildingStorey storey = building.FindStorey(level)!;
                    PlanOpening sized = storey.Openings.Find(found => found.Id == id)!;
                    sized.Width = width;
                    sized.Height = height;
                    PlanEdits.KeepOpenings(storey);
                });
                break;
            case PlanPickKind.Stairs when start.Stairs is { } stairs:
                float wide = Math.Clamp(SnapLength(stairs.Width * most), MinStairsWidth, MaxStairsWidth);
                Edit($"Stairs · {Format(wide, "0.00")} m wide", building => building.Stairs.Find(found => found.Id == id)!.Width = wide);
                break;
            case PlanPickKind.Elevator when start.Elevator is { } elevator:
                float across = Math.Clamp(SnapLength(elevator.Size * most), MinElevatorSize, MaxElevatorSize);
                Edit($"Elevator · {Format(across, "0.00")} m across", building => building.Elevators.Find(found => found.Id == id)!.Size = across);
                break;
            case PlanPickKind.Shot:
                float seen = Math.Max(start.Values.X * most, MinShotSize);
                Edit($"Shot · {Format(seen, "0.0")} m", building => building.Shots.Find(found => found.Id == id)!.Size = seen);
                break;
            default:
                break;
        }
    }

    /// <summary>The scale of the axis that was dragged furthest from 1.</summary>
    private static float GetMostChanged(Vector3 scale)
    {
        float most = scale.X;
        if (MathF.Abs(scale.Y - 1) > MathF.Abs(most - 1))
        {
            most = scale.Y;
        }

        if (MathF.Abs(scale.Z - 1) > MathF.Abs(most - 1))
        {
            most = scale.Z;
        }

        return most;
    }

    private void DrawGizmo(OverlayBuilder overlay)
    {
        if (TryGetGizmoTarget(out Transform target))
        {
            Gizmo.Draw(overlay, _camera, target, _height);
        }
    }
}
