using System.Numerics;

using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Editor.Core.Buildings;

public sealed partial class BuildingEditor
{
    private const float MinStairsWidth = 0.6f;
    private const float MaxStairsWidth = 4;
    private const float MinElevatorSize = 0.8f;
    private const float MaxElevatorSize = 4;

    private Vector3? _ghostTag;

    private bool BeginOnWall(BuildingStorey storey, PlanHit? hit, Vector2 point, Drag drag)
    {
        PlanWall? wall = null;
        if (hit is { Pick.Kind: PlanPickKind.Wall } onWall)
        {
            wall = PlanGeometry.FindWall(storey, onWall.Pick.Id);
            _dragHeight = onWall.Point.Y;
            _dragStart = new Vector2(onWall.Point.X, onWall.Point.Z);
        }
        else if (PlanEdits.FindWall(storey, point, Reach) is { } near)
        {
            wall = near.Wall;
        }

        if (wall is null)
        {
            Warn(drag == Drag.Push ? "Click a wall to push it, then drag it in or out." : "Click a wall to bend it, then drag its middle sideways.");
            return false;
        }

        Picked = new PlanPick(PlanPickKind.Wall, _level, wall.Id);
        HoldGhost(_file()!, storey, Picked);
        _drag = drag;
        _dragValue = 0;
        if (drag == Drag.Arc)
        {
            _dragValue = wall.Bulge;
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
        Push(_level, pick.Id, step);
    }

    /// <summary>Holds the storey's wall lengths and room areas from before the pending edit, for the Tool tab's Follows.</summary>
    private void Remember()
    {
        _before.Clear();
        if (_file()?.FindStorey(_level) is not { } storey)
        {
            return;
        }

        foreach (PlanWall wall in storey.Walls)
        {
            List<Vector2> path = PlanGeometry.GetPath(storey, wall);
            _before[$"wall.{wall.Id}"] = PlanGeometry.GetLength(path);
        }

        foreach (RoomShape room in PlanGeometry.FindRooms(storey))
        {
            if (room.Record >= 0)
            {
                _before[$"room.{storey.Rooms[room.Record].Name}"] = room.Area;
            }
        }
    }

    /// <summary>Holds the outline of what is about to be edited where it stands, shown dashed until the edit is applied or cancelled.</summary>
    private void HoldGhost(BuildingFile file, BuildingStorey storey, PlanPick pick)
    {
        if (_grouped)
        {
            return;
        }

        _ghost.Clear();
        _ghostTag = null;
        float floor = file.GetFloorHeight(_level) + 0.02f;
        switch (pick.Kind)
        {
            case PlanPickKind.Wall when PlanGeometry.FindWall(storey, pick.Id) is { } wall:
                _ghost.Add(GetWallLoop(storey, wall, floor));
                break;
            case PlanPickKind.Opening when storey.Openings.Find(found => found.Id == pick.Id) is { } opening:
                if (GetOpeningLoop(file, storey, opening, 0, 1) is { } loop)
                {
                    _ghost.Add(loop);
                    _ghostTag = loop[loop.Length / 2];
                }

                break;
            case PlanPickKind.Pillar when storey.Pillars.Find(found => found.Id == pick.Id) is { } pillar:
                _ghost.Add(GetSquare(new Vector2(pillar.X, pillar.Z), pillar.Size / 2, floor));
                break;
            case PlanPickKind.Stairs when BuildingRegions.GetStairs(file, pick.Id) is { } stairs:
                _ghost.Add(GetFootprint(stairs, 0.02f));
                break;
            case PlanPickKind.Elevator when file.Elevators.Find(found => found.Id == pick.Id) is { } elevator:
                _ghost.Add(GetElevatorSquare(file, elevator, _level, 0.02f));
                break;
            default:
                break;
        }
    }

    /// <summary>The wall's outline round its two faces at a height, mitred as it is built.</summary>
    private static Vector3[] GetWallLoop(BuildingStorey storey, PlanWall wall, float height)
    {
        List<Vector2> path = PlanGeometry.GetPath(storey, wall);
        (List<Vector2> left, List<Vector2> right) = PlanGeometry.GetSides(storey, wall, path, PlanGeometry.GetThickness(storey, wall));
        List<Vector3> loop = [];
        foreach (Vector2 point in left)
        {
            loop.Add(new Vector3(point.X, height, point.Y));
        }

        for (int index = right.Count - 1; index >= 0; index--)
        {
            loop.Add(new Vector3(right[index].X, height, right[index].Y));
        }

        return [.. loop];
    }

    /// <summary>
    /// An opening's outline on its wall, grown by a margin, on the wall's outer face (side 1), its inner face (-1) or its middle (0);
    /// a hole's on the floor. Null when its wall is gone.
    /// </summary>
    private static Vector3[]? GetOpeningLoop(BuildingFile file, BuildingStorey storey, PlanOpening opening, float grow, float side)
    {
        if (opening.Kind == OpeningKind.Floor)
        {
            float floor = file.GetFloorHeight(storey.Level) + 0.02f;
            Vector2 half = new((opening.Width / 2) + grow, (opening.Height / 2) + grow);
            Vector2 middle = new(opening.X, opening.Z);
            return
            [
                new Vector3(middle.X - half.X, floor, middle.Y - half.Y),
                new Vector3(middle.X + half.X, floor, middle.Y - half.Y),
                new Vector3(middle.X + half.X, floor, middle.Y + half.Y),
                new Vector3(middle.X - half.X, floor, middle.Y + half.Y),
            ];
        }

        if (!OpeningShape.TryGetFrame(file, storey, opening, out OpeningFrame frame))
        {
            return null;
        }

        List<Vector2> profile = OpeningShape.GetProfile(opening, grow);
        float face = side * ((frame.WallThickness / 2) + 0.01f);
        Vector3[] loop = new Vector3[profile.Count];
        for (int index = 0; index < profile.Count; index++)
        {
            loop[index] = frame.Origin + (frame.Along * profile[index].X) + (Vector3.UnitY * profile[index].Y) + (frame.Out * face);
        }

        return loop;
    }

    private static Vector3[] GetSquare(Vector2 middle, float half, float height)
    {
        return
        [
            new Vector3(middle.X - half, height, middle.Y - half),
            new Vector3(middle.X + half, height, middle.Y - half),
            new Vector3(middle.X + half, height, middle.Y + half),
            new Vector3(middle.X - half, height, middle.Y + half),
        ];
    }

    /// <summary>The foot of a region's box, lifted a little, in the building's space.</summary>
    private static Vector3[] GetFootprint(in BuildingRegion region, float lift)
    {
        Matrix4x4 toBuilding = region.Place.ToMatrix();
        Vector3 half = region.Size / 2;
        Vector3 middle = region.Middle;
        Vector3[] corners =
        [
            new(middle.X - half.X, lift, middle.Z - half.Z),
            new(middle.X + half.X, lift, middle.Z - half.Z),
            new(middle.X + half.X, lift, middle.Z + half.Z),
            new(middle.X - half.X, lift, middle.Z + half.Z),
        ];
        for (int index = 0; index < corners.Length; index++)
        {
            corners[index] = Vector3.Transform(corners[index], toBuilding);
        }

        return corners;
    }

    /// <summary>An elevator's square on a level's floor, lifted a little, turned as the elevator is.</summary>
    private static Vector3[] GetElevatorSquare(BuildingFile file, BuildingElevator elevator, int level, float lift)
    {
        Transform place = BuildingMaker.GetElevatorPlace(file, elevator, level);
        Matrix4x4 toBuilding = place.ToMatrix();
        float half = elevator.Size / 2;
        Vector3[] corners = [new(-half, lift, -half), new(half, lift, -half), new(half, lift, half), new(-half, lift, half)];
        for (int index = 0; index < corners.Length; index++)
        {
            corners[index] = Vector3.Transform(corners[index], toBuilding);
        }

        return corners;
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
        string side = bulge < 0 ? "out" : "in";
        string name = $"Arc · bulge {Format(MathF.Abs(bulge), "0.00")} m {side}";
        Edit(name, building => PlanEdits.SetBulge(building.FindStorey(level)!, id, bulge));
    }

    /// <summary>Takes a corner by its foot or its head, whichever shows nearer the pointer, and drags it at that height from where it was taken.</summary>
    private bool PressCorner(BuildingStorey storey, PlanHit? hit, Ray ray, Vector2 pixel, float floor)
    {
        PlanCorner? corner = null;
        if (hit is { Pick.Kind: PlanPickKind.Corner } onCorner)
        {
            corner = PlanGeometry.FindCorner(storey, onCorner.Pick.Id);
        }

        corner ??= PlanEdits.FindCorner(storey, _dragStart, Reach);
        if (corner is null)
        {
            Warn("Click a corner where walls meet, then drag it.");
            return false;
        }

        float top = floor + storey.Height - storey.Floor;
        float toFoot = GetPixelDistance(new Vector3(corner.X, floor, corner.Z), pixel);
        float toHead = GetPixelDistance(new Vector3(corner.X, top, corner.Z), pixel);
        if (toHead < toFoot && TryGetPlanePoint(ray, top, out Vector2 atHead))
        {
            _dragHeight = top;
            _dragStart = atHead;
        }

        _grabOffset = new Vector2(corner.X, corner.Z) - _dragStart;
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

    private bool PressSplit(BuildingStorey storey, PlanHit? hit, Vector2 point)
    {
        (PlanWall Wall, float Along)? found = FindWallAt(storey, hit, point);
        if (found is not { } on)
        {
            Warn("Click a wall where it should be split.");
            return false;
        }

        int level = _level;
        int id = on.Wall.Id;
        float along = SnapLength(on.Along);
        Edit("Split wall", building => PlanEdits.SplitWall(building.FindStorey(level)!, id, along));
        Picked = new PlanPick(PlanPickKind.Wall, _level, id);
        Changed?.Invoke();
        return true;
    }

    /// <summary>The wall the ray met and how far along it, else the one within reach of the point on the floor.</summary>
    private static (PlanWall Wall, float Along)? FindWallAt(BuildingStorey storey, PlanHit? hit, Vector2 point)
    {
        if (hit is { Pick.Kind: PlanPickKind.Wall } onWall && PlanGeometry.FindWall(storey, onWall.Pick.Id) is { } wall)
        {
            List<Vector2> path = PlanGeometry.GetPath(storey, wall);
            Vector2 met = new(onWall.Point.X, onWall.Point.Z);
            (float along, _) = PlanGeometry.Project(path, met);
            return (wall, along);
        }

        return PlanEdits.FindWall(storey, point, Reach);
    }

    private bool PressPillar(BuildingStorey storey, PlanHit? hit, Vector2 point)
    {
        PlanPillar? existing = null;
        if (hit is { Pick.Kind: PlanPickKind.Pillar } onPillar)
        {
            existing = storey.Pillars.Find(pillar => pillar.Id == onPillar.Pick.Id);
        }

        if (existing is not null)
        {
            if (Picked is not { Kind: PlanPickKind.Pillar } held || held.Id != existing.Id)
            {
                Apply();
            }

            Picked = new PlanPick(PlanPickKind.Pillar, _level, existing.Id);
            HoldGhost(_file()!, storey, Picked);
            _drag = Drag.Pillar;
            _dragHeight = _file()!.GetFloorHeight(_level);
            Changed?.Invoke();
            return true;
        }

        Apply();
        int level = _level;
        Vector2 at = SnapPoint(point);
        float size = PillarSize;
        bool round = PillarRound;
        int id = storey.NextId();
        Edit("Pillar", building => PlanEdits.AddPillar(building.FindStorey(level)!, at, size, round));
        Picked = new PlanPick(PlanPickKind.Pillar, _level, id);
        _drag = Drag.Pillar;
        Changed?.Invoke();
        return true;
    }

    private void DragPillar()
    {
        if (Picked is not { Kind: PlanPickKind.Pillar } pick)
        {
            return;
        }

        int level = _level;
        int id = pick.Id;
        Vector2 at = _dragEnd;
        Edit($"Pillar · {Format(at.X, "0.00")}, {Format(at.Y, "0.00")}", building => MovePillar(building, level, id, at));
    }

    private static void MovePillar(BuildingFile building, int level, int id, Vector2 point)
    {
        PlanPillar pillar = building.FindStorey(level)!.Pillars.Find(found => found.Id == id)!;
        pillar.X = point.X;
        pillar.Z = point.Y;
    }

    private bool PressOpening(BuildingFile file, BuildingStorey storey, PlanHit? hit, Vector2 point)
    {
        if (hit is { Pick.Kind: PlanPickKind.Opening } onOpening && storey.Openings.Find(opening => opening.Id == onOpening.Pick.Id) is { } existing)
        {
            if (Picked?.Id != existing.Id || Picked.Kind is not (PlanPickKind.Opening or PlanPickKind.Handle))
            {
                Apply();
            }

            Picked = new PlanPick(PlanPickKind.Opening, _level, existing.Id);
            HoldGhost(file, storey, Picked);
            _drag = Drag.Along;
            _grab = 0;
            if (existing.Kind != OpeningKind.Floor && PlanGeometry.FindWall(storey, existing.Wall) is { } wall)
            {
                Vector2 met = new(onOpening.Point.X, onOpening.Point.Z);
                (float along, _) = PlanGeometry.Project(PlanGeometry.GetPath(storey, wall), met);
                _grab = along - existing.Along;
                _dragHeight = onOpening.Point.Y;
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
            List<Vector2> outline = PlanGeometry.GetOutline(storey);
            if (!Polygons.Contains(outline, at))
            {
                Warn("A hole needs a floor under it. Click inside the storey.");
                return true;
            }

            Edit("Hole", building => PlanEdits.AddHole(building.FindStorey(level)!, at, 1.2f, 1.2f));
        }
        else
        {
            if (FindWallAt(storey, hit, point) is not { } on)
            {
                Warn($"A {kind.ToString().ToLowerInvariant()} is cut into a wall. Click the wall it goes in.");
                return true;
            }

            int wallId = on.Wall.Id;
            float along = SnapLength(on.Along);
            Edit(kind.ToString(), building => PlanEdits.AddOpening(building.FindStorey(level)!, wallId, along, kind));
            if (hit is { Pick.Kind: PlanPickKind.Wall } onWall)
            {
                _dragHeight = onWall.Point.Y;
            }
        }

        Picked = new PlanPick(PlanPickKind.Opening, level, id);
        _drag = Drag.Along;
        _grab = 0;
        if (file.FindStorey(level)?.Openings.Find(opening => opening.Id == id) is { } added && !OpeningShape.Fits(file, file.FindStorey(level)!, added))
        {
            Warn($"{added.Name} doesn't fit on this wall as it is. Drag it along, or make it smaller in the Tool tab.");
        }

        Changed?.Invoke();
        return true;
    }

    private void DragAlong(BuildingFile file, BuildingStorey storey, Ray ray, Vector2 point)
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

        PlanHit? met = PlanPicker.Pick(file, _level, ray, static kind => kind == PlanPickKind.Wall);
        if (met is { } other && other.Pick.Id != opening.Wall && PlanGeometry.FindWall(storey, other.Pick.Id) is { } otherWall)
        {
            Vector2 onOther = new(other.Point.X, other.Point.Z);
            (float otherAlong, _) = PlanGeometry.Project(PlanGeometry.GetPath(storey, otherWall), onOther);
            int wallId = otherWall.Id;
            float placed = SnapLength(otherAlong);
            _grab = 0;
            _dragHeight = other.Point.Y;
            Edit($"{opening.Name} · on another wall", building => MoveOpening(building, level, id, wallId, placed));
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
        if (Picked is not { Kind: PlanPickKind.Opening } pick || storey.Openings.Find(opening => opening.Id == pick.Id) is not { } opening
            || !OpeningShape.TryGetFrame(file, storey, opening, out OpeningFrame frame))
        {
            return;
        }

        float perPixel = GetUnitsPerPixel(frame.Origin);
        float lift = SnapLength(Math.Max(_dragValue + ((_pixelStart.Y - pixel.Y) * perPixel), 0));
        if (MathF.Abs(lift - opening.Lift) < 1e-4f)
        {
            return;
        }

        int level = _level;
        int id = pick.Id;
        Edit($"{opening.Name} · lift {Format(lift, "0.00")} m", building => building.FindStorey(level)!.Openings.Find(found => found.Id == id)!.Lift = lift);
    }

    /// <summary>Slides a door's handle over its leaf: in from the leaf's free edge and up from the door's foot, where the ray meets the wall.</summary>
    private void DragHandle(BuildingFile file, BuildingStorey storey, Ray ray)
    {
        if (Picked is not { Kind: PlanPickKind.Handle } pick || storey.Openings.Find(opening => opening.Id == pick.Id) is not { } opening
            || !OpeningShape.TryGetFrame(file, storey, opening, out OpeningFrame frame) || !ray.IntersectPlane(frame.Origin, frame.Out, out Vector3 met))
        {
            return;
        }

        Vector3 from = met - frame.Origin;
        SetHandle(opening, frame, Vector3.Dot(from, frame.Along), from.Y);
    }

    /// <summary>Puts a door's handle at a place on its wall: along it from the opening's middle and up from its foot.</summary>
    private void SetHandle(PlanOpening opening, in OpeningFrame frame, float along, float up)
    {
        DoorLeaf leaf = OpeningShape.MakeLeaf(frame, opening);
        float hinge = Vector3.Dot(leaf.Pivot - frame.Origin, frame.Along);
        float free = Vector3.Dot(leaf.Pivot + leaf.Free - frame.Origin, frame.Along);
        float reach = MathF.Abs(free - hinge);
        float inset = Math.Clamp((along - free) * MathF.Sign(hinge - free), 0, reach);
        float height = Math.Clamp(up, 0, opening.Height);
        if (SnapOn)
        {
            inset = MathF.Round(inset / HandleStep) * HandleStep;
            height = MathF.Round(height / HandleStep) * HandleStep;
        }

        if (MathF.Abs(inset - opening.HandleInset) < 1e-4f && MathF.Abs(height - opening.HandleHeight) < 1e-4f)
        {
            return;
        }

        int level = _level;
        int id = opening.Id;
        string name = $"Handle · {Format(height, "0.00")} m up, {Format(inset, "0.00")} m in";
        Edit(name, building =>
        {
            PlanOpening door = building.FindStorey(level)!.Openings.Find(found => found.Id == id)!;
            door.HandleInset = inset;
            door.HandleHeight = height;
        });
    }

    /// <summary>The steps a door's handle snaps to, in metres.</summary>
    private const float HandleStep = 0.05f;

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

    private bool PressStairs(BuildingFile file, PlanHit? hit, Vector2 point)
    {
        if (hit is { Pick.Kind: PlanPickKind.Stairs } onStairs && file.Stairs.Find(found => found.Id == onStairs.Pick.Id) is { } stairs)
        {
            if (Picked?.Id != stairs.Id || Picked.Kind != PlanPickKind.Stairs)
            {
                Apply();
            }

            Picked = new PlanPick(PlanPickKind.Stairs, _level, stairs.Id);
            HoldGhost(file, file.FindStorey(_level)!, Picked);
            _drag = Drag.Stairs;
            _dragHeight = onStairs.Point.Y;
            _dragStart = SnapPoint(new Vector2(onStairs.Point.X, onStairs.Point.Z)) - new Vector2(stairs.X, stairs.Z);
            Changed?.Invoke();
            return true;
        }

        Apply();
        int level = _level;
        if (file.FindStorey(level + 1) is null)
        {
            Warn("Stairs climb to the storey above, and this is the top one. Add a storey above first (the + under the storey list).");
            return true;
        }

        Vector2 foot = SnapPoint(point);
        float heading = StairsHeading;
        StairsKind kind = NewStairs;
        int id = 1 + file.Stairs.Select(static found => found.Id).DefaultIfEmpty(0).Max();
        Edit("Stairs", building => BuildingTools.AddStairs(building, level, foot, heading, kind));
        Picked = new PlanPick(PlanPickKind.Stairs, level, id);
        _drag = Drag.StairsHeading;
        _grab = 0;
        Changed?.Invoke();
        return true;
    }

    private bool PressElevator(BuildingFile file, PlanHit? hit, Vector2 point)
    {
        if (hit is { Pick.Kind: PlanPickKind.Elevator } onShaft && file.Elevators.Find(found => found.Id == onShaft.Pick.Id) is { } elevator)
        {
            if (Picked?.Id != elevator.Id || Picked.Kind != PlanPickKind.Elevator)
            {
                Apply();
            }

            Picked = new PlanPick(PlanPickKind.Elevator, _level, elevator.Id);
            HoldGhost(file, file.FindStorey(_level)!, Picked);
            _drag = Drag.Elevator;
            _dragStart = SnapPoint(point) - new Vector2(elevator.X, elevator.Z);
            Changed?.Invoke();
            return true;
        }

        Apply();
        if (file.Storeys.Count < 2)
        {
            Warn("An elevator travels between storeys, and this building has one. Add a storey or a cellar first.");
            return true;
        }

        Vector2 place = SnapPoint(point);
        int id = 1 + file.Elevators.Select(static found => found.Id).DefaultIfEmpty(0).Max();
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

    /// <summary>Turns picked stairs about their foot toward the point, or sets their width or first run from it.</summary>
    private void DragStairsShape(BuildingFile file, Vector2 point)
    {
        if (Picked is not { Kind: PlanPickKind.Stairs } pick || file.Stairs.Find(found => found.Id == pick.Id) is not { } stairs)
        {
            return;
        }

        int id = pick.Id;
        Vector2 fromFoot = point - new Vector2(stairs.X, stairs.Z);
        if (_drag == Drag.StairsHeading)
        {
            if (fromFoot.Length() < TurnFrom)
            {
                return;
            }

            float toward = float.RadiansToDegrees(MathF.Atan2(fromFoot.X, fromFoot.Y));
            float heading = SnapTurn(toward - _grab);
            if (MathF.Abs(heading - stairs.Heading) < 1e-3f)
            {
                return;
            }

            StairsHeading = heading;
            Edit($"Stairs · climbing {Format(heading, "0")}°", building => building.Stairs.Find(found => found.Id == id)!.Heading = heading);
            return;
        }

        Transform place = BuildingMaker.GetStairsPlace(file, stairs);
        Quaternion back = Quaternion.Inverse(place.Rotation);
        Vector3 local = Vector3.Transform(new Vector3(fromFoot.X, 0, fromFoot.Y), back);
        if (_drag == Drag.StairsWidth)
        {
            float width = Math.Clamp(SnapLength(MathF.Abs(local.X) * 2), MinStairsWidth, MaxStairsWidth);
            if (MathF.Abs(width - stairs.Width) >= 1e-4f)
            {
                Edit($"Stairs · {Format(width, "0.00")} m wide", building => building.Stairs.Find(found => found.Id == id)!.Width = width);
            }

            return;
        }

        float run = Math.Max(SnapLength(local.Z), stairs.Tread);
        if (MathF.Abs(run - stairs.FirstRun) >= 1e-4f)
        {
            Edit($"Stairs · first run {Format(run, "0.00")} m", building => building.Stairs.Find(found => found.Id == id)!.FirstRun = run);
        }
    }

    private void DragElevatorSize(BuildingFile file, Vector2 point)
    {
        if (Picked is not { Kind: PlanPickKind.Elevator } pick || file.Elevators.Find(found => found.Id == pick.Id) is not { } elevator)
        {
            return;
        }

        Transform place = BuildingMaker.GetElevatorPlace(file, elevator, _level);
        Quaternion back = Quaternion.Inverse(place.Rotation);
        Vector3 local = Vector3.Transform(new Vector3(point.X - elevator.X, 0, point.Y - elevator.Z), back);
        float reach = Math.Max(MathF.Abs(local.X), MathF.Abs(local.Z));
        float size = Math.Clamp(SnapLength(reach * 2), MinElevatorSize, MaxElevatorSize);
        if (MathF.Abs(size - elevator.Size) < 1e-4f)
        {
            return;
        }

        int id = pick.Id;
        Edit($"Elevator · {Format(size, "0.00")} m across", building => building.Elevators.Find(found => found.Id == id)!.Size = size);
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

    private bool PressCamera(BuildingFile file, BuildingStorey storey, PlanHit? hit, Vector2 point, Vector2 pixel)
    {
        if (AddingTrigger && Picked is { Kind: PlanPickKind.Shot } shotPick)
        {
            ShotTrigger trigger = FindTrigger(storey, hit, point);
            if (trigger.Target == TriggerTarget.Room && string.IsNullOrEmpty(trigger.Room))
            {
                Warn("A trigger is a room, a door, stairs or an elevator. Click one of them.");
                return true;
            }

            int id = shotPick.Id;
            Edit("Trigger", building => building.Shots.Find(shot => shot.Id == id)?.Triggers.Add(trigger));
            Apply();
            AddingTrigger = false;
            Changed?.Invoke();
            return true;
        }

        if (hit is { Pick.Kind: PlanPickKind.Shot } onShot)
        {
            Apply();
            Picked = onShot.Pick;
            Changed?.Invoke();
            return true;
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

    /// <summary>What a click sets off: the opening, stairs or elevator it met, else the room the point is in.</summary>
    private ShotTrigger FindTrigger(BuildingStorey storey, PlanHit? hit, Vector2 point)
    {
        switch (hit?.Pick.Kind)
        {
            case PlanPickKind.Opening:
                return new ShotTrigger { Target = TriggerTarget.Opening, Level = _level, Id = hit.Value.Pick.Id, When = TriggerMoment.Both, Change = TriggerChange.Cut };
            case PlanPickKind.Stairs:
                return new ShotTrigger { Target = TriggerTarget.Stairs, Level = _level, Id = hit.Value.Pick.Id };
            case PlanPickKind.Elevator:
                return new ShotTrigger { Target = TriggerTarget.Elevator, Level = _level, Id = hit.Value.Pick.Id };
            case PlanPickKind.Room:
                return new ShotTrigger { Target = TriggerTarget.Room, Level = _level, Room = hit.Value.Pick.Room };
            default:
                return new ShotTrigger { Target = TriggerTarget.Room, Level = _level, Room = FindRoomName(storey, point) };
        }
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
}
