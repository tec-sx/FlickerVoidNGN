using System.Numerics;

using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Buildings;

public sealed partial class BuildingEditor
{
    /// <summary>
    /// The labels over the viewport: the storey's rooms with their areas (their shots with the camera tool), the picked wall's length
    /// and what its tool is doing to it, openings with where they stand, stairs with their steps and the floor they open, elevators
    /// with their stops, shots with what sets them off, and the tag of the drag in hand.
    /// </summary>
    public IReadOnlyList<PlanLabel> GetLabels()
    {
        List<PlanLabel> labels = [];
        if (_file() is not { } file || file.FindStorey(_level) is not { } storey)
        {
            return labels;
        }

        Matrix4x4 place = _place();
        float floor = file.GetFloorHeight(_level);
        if (!ShowsOpenStorey)
        {
            AddRoomLabels(labels, file, storey, place, floor);
        }

        if (Picked is { Kind: PlanPickKind.Wall } wallPick && PlanGeometry.FindWall(storey, wallPick.Id) is { } wall)
        {
            AddWallLabels(labels, storey, wall, place, floor);
        }

        if (_tool == BuildingTool.Opening)
        {
            AddOpeningLabels(labels, file, storey, place);
        }

        if (ShowsOpenStorey)
        {
            AddStairsLabels(labels, file, place);
            AddElevatorLabels(labels, file, place);
        }

        if (_tool == BuildingTool.Camera)
        {
            AddShotLabels(labels, file, place);
        }

        if (_ghostTag is { } ghost && _grouped)
        {
            labels.Add(new PlanLabel(Vector3.Transform(ghost, place), "where it was", string.Empty, PlanLabelKind.Dimension));
        }

        AddDragLabel(labels, storey, place, floor);
        return labels;
    }

    /// <summary>What a storey is called: Cellar, Ground, Storey 1 and up.</summary>
    public static string GetStoreyName(int level)
    {
        if (level < 0)
        {
            return "Cellar";
        }

        return level == 0 ? "Ground" : $"Storey {level}";
    }

    private static Vector3 ToWorld(in Matrix4x4 place, Vector2 point, float height)
    {
        Vector3 local = new(point.X, height, point.Y);
        return Vector3.Transform(local, place);
    }

    private void AddRoomLabels(List<PlanLabel> labels, BuildingFile file, BuildingStorey storey, in Matrix4x4 place, float floor)
    {
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
    }

    private void AddWallLabels(List<PlanLabel> labels, BuildingStorey storey, PlanWall wall, in Matrix4x4 place, float floor)
    {
        List<Vector2> path = PlanGeometry.GetPath(storey, wall);
        float length = PlanGeometry.GetLength(path);
        (Vector2 middle, _) = PlanGeometry.GetAlong(path, length / 2);
        Vector2 normal = PlanEdits.GetNormal(storey, wall);
        float thickness = PlanGeometry.GetThickness(storey, wall);
        float top = floor + PlanGeometry.GetHeight(storey, wall);
        if (_tool == BuildingTool.Arc)
        {
            string bulge = $"Bulge {Format(MathF.Abs(wall.Bulge), "0.00")} m";
            labels.Add(new PlanLabel(ToWorld(place, middle, top + 0.35f), bulge, $"{wall.Segments} segments", PlanLabelKind.Drag));
            return;
        }

        labels.Add(new PlanLabel(ToWorld(place, middle - (normal * (thickness + 0.4f)), floor), $"{Format(length, "0.00")} m", string.Empty, PlanLabelKind.Dimension));
        if (_tool != BuildingTool.PushWall)
        {
            return;
        }

        (_, Vector3 tip) = GetPushArrow(storey, wall, floor);
        Vector3 beside = Vector3.Transform(tip + new Vector3(normal.X, 0, normal.Y) * 0.35f, place);
        if (_grouped && MathF.Abs(Pushed) > 1e-4f)
        {
            labels.Add(new PlanLabel(beside, $"{Format(Pushed, "+0.00;-0.00")} m", $"wall stays {Format(thickness, "0.00")} m", PlanLabelKind.Drag));
        }
        else
        {
            labels.Add(new PlanLabel(beside, "Drag the arrow", "or the wall itself", PlanLabelKind.Dimension));
        }
    }

    private void AddOpeningLabels(List<PlanLabel> labels, BuildingFile file, BuildingStorey storey, in Matrix4x4 place)
    {
        foreach (PlanOpening opening in storey.Openings)
        {
            bool picked = Picked is { Kind: PlanPickKind.Opening or PlanPickKind.Handle } pick && pick.Id == opening.Id;
            if (opening.Kind == OpeningKind.Floor)
            {
                Vector3 hole = new(opening.X, file.GetFloorHeight(_level) + 0.3f, opening.Z);
                labels.Add(new PlanLabel(Vector3.Transform(hole, place), opening.Name, "cut out of this floor", PlanLabelKind.Cut));
                continue;
            }

            if (!OpeningShape.TryGetFrame(file, storey, opening, out OpeningFrame frame))
            {
                continue;
            }

            Vector3 above = frame.Origin + (Vector3.UnitY * (opening.Height + 0.25f));
            if (!picked)
            {
                labels.Add(new PlanLabel(Vector3.Transform(above, place), opening.Name, $"lift {Format(opening.Lift, "0.00")}", PlanLabelKind.Dimension));
                continue;
            }

            if (Picked!.Kind == PlanPickKind.Handle)
            {
                string grip = $"Handle {Format(opening.HandleHeight, "0.00")} m up";
                labels.Add(new PlanLabel(Vector3.Transform(above, place), grip, $"{Format(opening.HandleInset, "0.00")} m in from the edge", PlanLabelKind.Drag));
                continue;
            }

            string along = $"{Format(opening.Along, "0.00")} m along";
            string hint = OpeningShape.Fits(file, storey, opening) ? "drag or click another wall" : "doesn't fit here";
            labels.Add(new PlanLabel(Vector3.Transform(above, place), along, hint, PlanLabelKind.Drag));
            string lifted = opening.Lift < 1e-4f ? "on the floor" : "above the floor";
            Vector3 foot = frame.Origin - (Vector3.UnitY * 0.3f);
            labels.Add(new PlanLabel(Vector3.Transform(foot, place), $"Lift {Format(opening.Lift, "0.00")} m", lifted, PlanLabelKind.Dimension));
        }
    }

    private void AddStairsLabels(List<PlanLabel> labels, BuildingFile file, in Matrix4x4 place)
    {
        foreach (BuildingStairs stairs in file.Stairs)
        {
            if (stairs.Level != _level)
            {
                continue;
            }

            StairsLayout layout = BuildingMaker.GetStairsLayout(file, stairs);
            Transform stand = BuildingMaker.GetStairsPlace(file, stairs);
            Matrix4x4 toWorld = stand.ToMatrix() * place;
            bool picked = Picked is { Kind: PlanPickKind.Stairs } pick && pick.Id == stairs.Id;
            string above = GetStoreyName(stairs.Level + 1);
            if (layout.Openings.Count > 0)
            {
                (Vector2 low, Vector2 high) = layout.Openings[0];
                Vector2 middle = (low + high) / 2;
                float climb = BuildingMaker.GetStairsHeight(file, stairs);
                Vector3 cut = Vector3.Transform(new Vector3(middle.X, climb + 0.3f, middle.Y), toWorld);
                labels.Add(new PlanLabel(cut, $"Opening in {above}'s floor", "cut live", PlanLabelKind.Cut));
            }

            if (!picked)
            {
                continue;
            }

            Vector3 top = GetStairsTop(layout) + (Vector3.UnitY * 0.45f);
            string meets = stairs.MeetsFloor ? $"top meets {above}" : $"top step under {above}";
            labels.Add(new PlanLabel(Vector3.Transform(top, toWorld), $"{layout.Steps} steps of {Format(layout.StepRise, "0.00")} m", meets, PlanLabelKind.Drag));
            if (_tool == BuildingTool.Stairs)
            {
                Vector3 foot = Vector3.Transform(new Vector3(0, 0, -0.9f), toWorld);
                labels.Add(new PlanLabel(foot, "foot, climbs this way", string.Empty, PlanLabelKind.Dimension));
            }
        }
    }

    private void AddElevatorLabels(List<PlanLabel> labels, BuildingFile file, in Matrix4x4 place)
    {
        foreach (BuildingElevator elevator in file.Elevators)
        {
            (int from, int to) = BuildingMaker.GetElevatorSpan(file, elevator);
            if (_level < from || _level > to)
            {
                continue;
            }

            float top = file.GetFloorHeight(to) + BuildingRegions.Headroom + 0.3f;
            Vector3 over = Vector3.Transform(new Vector3(elevator.X, top, elevator.Z), place);
            string buttons = elevator.CallButtons ? "call button on each floor" : "no call buttons";
            labels.Add(new PlanLabel(over, $"Elevator {elevator.Id}", $"{GetStoreyName(from)} to {GetStoreyName(to)}, {buttons}", PlanLabelKind.Dimension));
        }
    }

    private void AddShotLabels(List<PlanLabel> labels, BuildingFile file, in Matrix4x4 place)
    {
        foreach (BuildingShot shot in file.Shots)
        {
            bool picked = Picked is { Kind: PlanPickKind.Shot } pick && pick.Id == shot.Id;
            if (shot.Level != _level && !picked)
            {
                continue;
            }

            string count = shot.Triggers.Count == 1 ? "1 trigger" : $"{shot.Triggers.Count} triggers";
            labels.Add(new PlanLabel(Vector3.Transform(shot.Position, place), shot.Name, count, PlanLabelKind.Shot));
            if (!picked)
            {
                continue;
            }

            foreach (ShotTrigger trigger in shot.Triggers)
            {
                if (GetTriggerPoint(file, trigger) is { } at)
                {
                    labels.Add(new PlanLabel(Vector3.Transform(at, place), DescribeTrigger(file, trigger), DescribeChange(trigger), PlanLabelKind.Trigger));
                }
            }
        }
    }

    /// <summary>Where a trigger's tag stands: in its room, or over the middle of its box.</summary>
    private static Vector3? GetTriggerPoint(BuildingFile file, ShotTrigger trigger)
    {
        if (BuildingRegions.GetTrigger(file, trigger) is not { } region)
        {
            return null;
        }

        Vector3 middle = Vector3.Transform(region.Middle, region.Place.ToMatrix());
        if (trigger.Target == TriggerTarget.Room)
        {
            return new Vector3(middle.X, region.Place.Position.Y + 0.6f, middle.Z);
        }

        return middle + (Vector3.UnitY * ((region.Size.Y / 2) + 0.2f));
    }

    private static string DescribeTrigger(BuildingFile file, ShotTrigger trigger)
    {
        switch (trigger.Target)
        {
            case TriggerTarget.Opening:
                return file.FindStorey(trigger.Level)?.Openings.Find(found => found.Id == trigger.Id)?.Name ?? "Opening";
            case TriggerTarget.Stairs:
                return $"Stairs {trigger.Id}";
            case TriggerTarget.Elevator:
                return $"Elevator {trigger.Id}";
            default:
                return $"Room: {trigger.Room}";
        }
    }

    private static string DescribeChange(ShotTrigger trigger)
    {
        if (trigger.Change == TriggerChange.Cut)
        {
            return "cut";
        }

        return $"{trigger.Change.ToString().ToLowerInvariant()} {Format(trigger.Time, "0.0")} s";
    }

    /// <summary>The tag at the mouse of the drag in hand: a wall being drawn, a room, or the pending edit's name.</summary>
    private void AddDragLabel(List<PlanLabel> labels, BuildingStorey storey, in Matrix4x4 place, float floor)
    {
        float top = floor + storey.Height - storey.Floor;
        if (_drag == Drag.Draw)
        {
            string length = $"{Format(Vector2.Distance(_dragStart, _dragEnd), "0.00")} m";
            labels.Add(new PlanLabel(ToWorld(place, (_dragStart + _dragEnd) / 2, top + 0.4f), length, _joins ?? string.Empty, PlanLabelKind.Drag));
            return;
        }

        if (_drag == Drag.Rectangle)
        {
            Vector2 size = Vector2.Abs(_dragEnd - _dragStart);
            string text = $"{Format(size.X, "0.0")} × {Format(size.Y, "0.0")} m";
            labels.Add(new PlanLabel(ToWorld(place, (_dragStart + _dragEnd) / 2, floor), "New room", text, PlanLabelKind.Room));
            return;
        }

        bool tagged = _tool is BuildingTool.PushWall or BuildingTool.Arc or BuildingTool.Opening or BuildingTool.Stairs;
        if (_grouped && !tagged && _drag != Drag.None && !string.IsNullOrEmpty(PendingName))
        {
            labels.Add(new PlanLabel(ToWorld(place, _dragEnd, _dragHeight + 0.4f), PendingName, string.Empty, PlanLabelKind.Drag));
        }
    }
}
