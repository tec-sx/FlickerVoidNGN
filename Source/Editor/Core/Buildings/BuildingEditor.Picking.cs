using System.Numerics;

using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Editor.Core.Buildings;

public sealed partial class BuildingEditor
{
    /// <summary>How long the push arrow and the lift arrow are on the screen, in pixels.</summary>
    private const float ArrowPixels = 56;

    /// <summary>
    /// What a tool takes under the mouse: a corner or a shot near it on the screen, for the tools that take them, else the nearest
    /// thing of the tool's kinds the ray meets.
    /// </summary>
    private PlanHit? FindHit(BuildingFile file, BuildingStorey storey, Ray ray, Vector2 pixel, BuildingTool tool)
    {
        float floor = file.GetFloorHeight(_level);
        if (tool is BuildingTool.Select or BuildingTool.MoveCorner && FindCornerOnScreen(storey, floor, pixel) is { } corner)
        {
            PlanPick cornerPick = new(PlanPickKind.Corner, _level, corner.Id);
            return new PlanHit(cornerPick, 0, new Vector3(corner.X, floor, corner.Z));
        }

        if (tool == BuildingTool.Camera && FindShotOnScreen(file, pixel) is { } shot)
        {
            PlanPick shotPick = new(PlanPickKind.Shot, shot.Level, shot.Id);
            return new PlanHit(shotPick, 0, shot.Position);
        }

        return PlanPicker.Pick(file, _level, ray, kind => Takes(tool, kind));
    }

    /// <summary>Whether a tool takes things of a kind: the select and camera tools all, each other tool what it works on.</summary>
    private static bool Takes(BuildingTool tool, PlanPickKind kind)
    {
        switch (tool)
        {
            case BuildingTool.Select:
            case BuildingTool.Camera:
                return true;
            case BuildingTool.PushWall:
            case BuildingTool.Arc:
            case BuildingTool.SplitWall:
                return kind == PlanPickKind.Wall;
            case BuildingTool.Pillar:
                return kind == PlanPickKind.Pillar;
            case BuildingTool.Opening:
                return kind is PlanPickKind.Opening or PlanPickKind.Wall;
            case BuildingTool.Stairs:
                return kind == PlanPickKind.Stairs;
            case BuildingTool.Elevator:
                return kind == PlanPickKind.Elevator;
            default:
                return false;
        }
    }

    /// <summary>
    /// Why an opening can't stand where it is, in a few words for its tag and its row, or null when it can: it reaches past its wall
    /// or its floor, or its cut runs into another opening's.
    /// </summary>
    public static string? GetMisfit(BuildingFile file, BuildingStorey storey, PlanOpening opening)
    {
        if (!OpeningShape.Fits(file, storey, opening))
        {
            return "doesn't fit here";
        }

        if (OpeningShape.FindOverlapped(storey, opening) is { } other)
        {
            return $"runs into {other.Name}";
        }

        return null;
    }

    /// <summary>Whether what is picked stays picked as a tool is taken: what the tool works on, so a door is not left lit under the stairs tool.</summary>
    private static bool Keeps(BuildingTool tool, PlanPickKind kind)
    {
        switch (tool)
        {
            case BuildingTool.Select:
            case BuildingTool.Camera:
                return true;
            case BuildingTool.MoveCorner:
                return kind == PlanPickKind.Corner;
            case BuildingTool.Room:
                return kind == PlanPickKind.Room;
            case BuildingTool.Opening:
                return kind is PlanPickKind.Opening or PlanPickKind.Handle;
            case BuildingTool.Roof:
                return kind == PlanPickKind.Roof;
            default:
                return Takes(tool, kind);
        }
    }

    private PlanPick? FindHover(BuildingFile file, BuildingStorey storey, Ray ray, Vector2 pixel)
    {
        if (_tool == BuildingTool.Wall)
        {
            float floor = file.GetFloorHeight(_level);
            if (FindCornerOnScreen(storey, floor, pixel) is { } corner)
            {
                return new PlanPick(PlanPickKind.Corner, _level, corner.Id);
            }

            return null;
        }

        PlanHit? hit = FindHit(file, storey, ray, pixel, _tool);
        if (hit is null && _tool is BuildingTool.PushWall or BuildingTool.Arc or BuildingTool.SplitWall or BuildingTool.Opening
            && TryGetPlanePoint(ray, file.GetFloorHeight(_level), out Vector2 point) && PlanEdits.FindWall(storey, point, Reach) is { } near)
        {
            return new PlanPick(PlanPickKind.Wall, _level, near.Wall.Id);
        }

        return hit?.Pick;
    }

    /// <summary>The storey's corner whose foot or head shows nearest the pixel, within the grab's reach.</summary>
    private PlanCorner? FindCornerOnScreen(BuildingStorey storey, float floor, Vector2 pixel)
    {
        PlanCorner? best = null;
        float nearest = GrabPixels;
        float top = floor + storey.Height - storey.Floor;
        foreach (PlanCorner corner in storey.Corners)
        {
            float low = GetPixelDistance(new Vector3(corner.X, floor, corner.Z), pixel);
            float high = GetPixelDistance(new Vector3(corner.X, top, corner.Z), pixel);
            float distance = Math.Min(low, high);
            if (distance <= nearest)
            {
                nearest = distance;
                best = corner;
            }
        }

        return best;
    }

    private BuildingShot? FindShotOnScreen(BuildingFile file, Vector2 pixel)
    {
        foreach (BuildingShot shot in file.Shots)
        {
            if (GetPixelDistance(shot.Position, pixel) <= GrabPixels + 4)
            {
                return shot;
            }
        }

        return null;
    }

    /// <summary>How far from the pixel a point of the building shows on the screen.</summary>
    private float GetPixelDistance(Vector3 point, Vector2 pixel)
    {
        Vector3 world = Vector3.Transform(point, _place());
        Vector2 shown = _camera.Project(world, _width, _height);
        return Vector2.Distance(shown, pixel);
    }

    /// <summary>How far from the pixel a line between two points of the building shows on the screen.</summary>
    private float GetPixelDistance(Vector3 from, Vector3 to, Vector2 pixel)
    {
        Matrix4x4 place = _place();
        Vector2 first = _camera.Project(Vector3.Transform(from, place), _width, _height);
        Vector2 second = _camera.Project(Vector3.Transform(to, place), _width, _height);
        Vector2 along = second - first;
        float length = along.LengthSquared();
        if (length < 1e-6f)
        {
            return Vector2.Distance(first, pixel);
        }

        float share = Math.Clamp(Vector2.Dot(pixel - first, along) / length, 0, 1);
        return Vector2.Distance(first + (along * share), pixel);
    }

    /// <summary>How many metres of the building a pixel covers at a point of it.</summary>
    private float GetUnitsPerPixel(Vector3 point)
    {
        Vector3 world = Vector3.Transform(point, _place());
        return _camera.WorldUnitsPerPixel(world, _height);
    }

    /// <summary>The push arrow of a wall: from the middle of its outer face at the floor, out along its normal, as long on the screen as an arrow is.</summary>
    private (Vector3 From, Vector3 Tip) GetPushArrow(BuildingStorey storey, PlanWall wall, float floor)
    {
        List<Vector2> path = PlanGeometry.GetPath(storey, wall);
        (Vector2 middle, _) = PlanGeometry.GetAlong(path, PlanGeometry.GetLength(path) / 2);
        Vector2 normal = PlanEdits.GetNormal(storey, wall);
        Vector2 foot = middle + (normal * (PlanGeometry.GetThickness(storey, wall) / 2));
        Vector3 from = new(foot.X, floor + 0.02f, foot.Y);
        float length = GetUnitsPerPixel(from) * ArrowPixels;
        Vector3 tip = from + (new Vector3(normal.X, 0, normal.Y) * length);
        return (from, tip);
    }

    /// <summary>The lift arrow of an opening on its wall: from the middle of its foot on the wall's outer face, up.</summary>
    private (Vector3 Foot, Vector3 Tip) GetLiftArrow(in OpeningFrame frame)
    {
        Vector3 foot = frame.Origin + (frame.Out * ((frame.WallThickness / 2) + 0.01f));
        float length = GetUnitsPerPixel(foot) * ArrowPixels;
        return (foot, foot + (Vector3.UnitY * length));
    }

    /// <summary>The handles of a door's leaves, where a hand takes them, in the building's space.</summary>
    private static List<Vector3> GetDoorHandles(in OpeningFrame frame, PlanOpening opening)
    {
        List<Vector3> handles = [];
        if (opening.Kind != OpeningKind.Door)
        {
            return handles;
        }

        foreach (DoorLeaf leaf in OpeningShape.MakeLeaves(frame, opening, null))
        {
            handles.Add(leaf.Pivot + leaf.Handle);
        }

        return handles;
    }

    /// <summary>
    /// The squares that shape picked stairs, in the building's space: the one at the foot's side sets the width, the one at the end of
    /// the first run its length (an L or a U), and the one on the top step turns the flight about its foot.
    /// </summary>
    private static (Vector3 Width, Vector3? Run, Vector3 Top) GetStairsHandles(BuildingFile file, BuildingStairs stairs)
    {
        StairsLayout layout = BuildingMaker.GetStairsLayout(file, stairs);
        Transform place = BuildingMaker.GetStairsPlace(file, stairs);
        Matrix4x4 toBuilding = place.ToMatrix();
        Vector3 width = Vector3.Transform(new Vector3(stairs.Width / 2, 0, 0), toBuilding);
        Vector3 top = Vector3.Transform(GetStairsTop(layout), toBuilding);
        Vector3? run = null;
        if (stairs.Kind != StairsKind.Straight)
        {
            run = Vector3.Transform(new Vector3(0, 0, GetFirstRun(layout, stairs)), toBuilding);
        }

        return (width, run, top);
    }

    /// <summary>The middle of the top step, in the stairs' own space.</summary>
    private static Vector3 GetStairsTop(StairsLayout layout)
    {
        Vector3 top = Vector3.Zero;
        foreach ((Vector2 low, Vector2 high, float height) in layout.Blocks)
        {
            if (height >= top.Y)
            {
                Vector2 middle = (low + high) / 2;
                top = new Vector3(middle.X, height, middle.Y);
            }
        }

        return top;
    }

    /// <summary>How far the first run reaches before the landing, as the steps lie.</summary>
    private static float GetFirstRun(StairsLayout layout, BuildingStairs stairs)
    {
        float half = stairs.Width / 2;
        float reach = 0;
        foreach ((Vector2 low, Vector2 high, _) in layout.Blocks)
        {
            bool onRun = MathF.Abs(low.X + half) < 1e-3f && MathF.Abs(high.X - half) < 1e-3f && high.Y - low.Y <= stairs.Tread + 1e-3f;
            if (!onRun)
            {
                break;
            }

            reach = high.Y;
        }

        return reach;
    }

    /// <summary>The square at a picked elevator's corner on the storey's floor, which sizes it.</summary>
    private Vector3 GetElevatorHandle(BuildingFile file, BuildingElevator elevator)
    {
        Transform place = BuildingMaker.GetElevatorPlace(file, elevator, _level);
        float half = elevator.Size / 2;
        return Vector3.Transform(new Vector3(half, 0.02f, half), place.ToMatrix());
    }

    /// <summary>A press on a handle of what is picked: the push arrow, an opening's lift arrow or a door's handle, the squares of stairs and an elevator.</summary>
    private bool PressHandle(BuildingFile file, BuildingStorey storey, Vector2 pixel, Ray ray)
    {
        if (Picked is not { } pick || pick.Level != _level && pick.Kind != PlanPickKind.Elevator)
        {
            return false;
        }

        float floor = file.GetFloorHeight(_level);
        switch (pick.Kind)
        {
            case PlanPickKind.Wall when _tool == BuildingTool.PushWall && PlanGeometry.FindWall(storey, pick.Id) is { } wall:
                (Vector3 from, Vector3 tip) = GetPushArrow(storey, wall, floor);
                if (GetPixelDistance(from, tip, pixel) > GrabPixels || !TryGetPlanePoint(ray, floor, out Vector2 start))
                {
                    return false;
                }

                HoldGhost(file, storey, pick);
                _dragStart = start;
                _dragHeight = floor;
                _dragValue = 0;
                _drag = Drag.Push;
                return true;
            case PlanPickKind.Opening or PlanPickKind.Handle when _tool is BuildingTool.Select or BuildingTool.Opening:
                return PressOpeningHandle(file, storey, pick, pixel);
            case PlanPickKind.Stairs when _tool == BuildingTool.Stairs && file.Stairs.Find(found => found.Id == pick.Id) is { } stairs:
                return PressStairsHandle(file, storey, stairs, pixel);
            case PlanPickKind.Elevator when _tool == BuildingTool.Elevator && file.Elevators.Find(found => found.Id == pick.Id) is { } elevator:
                if (GetPixelDistance(GetElevatorHandle(file, elevator), pixel) > GrabPixels)
                {
                    return false;
                }

                HoldGhost(file, storey, pick);
                _drag = Drag.ElevatorSize;
                return true;
            default:
                return false;
        }
    }

    private bool PressOpeningHandle(BuildingFile file, BuildingStorey storey, PlanPick pick, Vector2 pixel)
    {
        if (storey.Openings.Find(found => found.Id == pick.Id) is not { Kind: not OpeningKind.Floor } opening
            || !OpeningShape.TryGetFrame(file, storey, opening, out OpeningFrame frame))
        {
            return false;
        }

        foreach (Vector3 handle in GetDoorHandles(frame, opening))
        {
            if (GetPixelDistance(handle, pixel) <= GrabPixels)
            {
                HoldGhost(file, storey, pick);
                Picked = new PlanPick(PlanPickKind.Handle, _level, opening.Id);
                _drag = Drag.Handle;
                Changed?.Invoke();
                return true;
            }
        }

        (Vector3 foot, Vector3 tip) = GetLiftArrow(frame);
        if (GetPixelDistance(foot, tip, pixel) > GrabPixels)
        {
            return false;
        }

        HoldGhost(file, storey, pick);
        Picked = new PlanPick(PlanPickKind.Opening, _level, opening.Id);
        _drag = Drag.Lift;
        _dragValue = opening.Lift;
        Changed?.Invoke();
        return true;
    }

    private bool PressStairsHandle(BuildingFile file, BuildingStorey storey, BuildingStairs stairs, Vector2 pixel)
    {
        (Vector3 width, Vector3? run, Vector3 top) = GetStairsHandles(file, stairs);
        Drag drag = Drag.None;
        if (GetPixelDistance(width, pixel) <= GrabPixels)
        {
            drag = Drag.StairsWidth;
        }
        else if (run is { } runHandle && GetPixelDistance(runHandle, pixel) <= GrabPixels)
        {
            drag = Drag.StairsRun;
        }
        else if (GetPixelDistance(top, pixel) <= GrabPixels)
        {
            drag = Drag.StairsHeading;
        }

        if (drag == Drag.None)
        {
            return false;
        }

        HoldGhost(file, storey, Picked!);
        _drag = drag;
        _grab = 0;
        if (drag == Drag.StairsHeading)
        {
            // The top step keeps its angle from the way the flight climbs, so an L turns about its foot without jumping.
            StairsLayout layout = BuildingMaker.GetStairsLayout(file, stairs);
            Vector3 local = GetStairsTop(layout);
            _grab = float.RadiansToDegrees(MathF.Atan2(local.X, local.Z));
            _dragHeight = top.Y;
        }

        return true;
    }
}
