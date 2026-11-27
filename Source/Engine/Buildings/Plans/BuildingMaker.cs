using System.Numerics;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Buildings.Plans;

/// <summary>
/// Makes a building's parts from its plans: each storey's floor (opened for stairs, shafts and holes), its outside walls and each inside
/// wall (with their openings cut), its pillars, door leaves on their hinges, window panes and frames; the stairs and their railings,
/// the elevators' platforms and railings, and the roof. The cuts need a Boolean subtraction, which the editor gives; without one the
/// walls and floors are left whole. Parts reshaped by hand are kept.
/// </summary>
public static class BuildingMaker
{
    public const float PlatformThickness = 0.1f;

    /// <summary>How far a shaft's opening reaches past its platform on every side: a hairline, so walkers step across it onto the platform.</summary>
    public const float ShaftClearance = 0.02f;

    private const float HoleReach = 0.1f;
    private const int RoundSides = 16;

    /// <summary>The offer action that opens and shuts a door.</summary>
    public static readonly Tag DoorAction = new("Interaction.Action.Toggle");

    /// <summary>How fast a door swings, in degrees a second.</summary>
    public const float DoorSpeed = 120;

    /// <summary>Makes every part of the building from its plans again; <paramref name="subtract"/> cuts one solid from another, or null.</summary>
    public static void Make(BuildingFile file, Func<SolidMesh, SolidMesh, SolidMesh?>? subtract)
    {
        Dictionary<int, List<SolidMesh>> holes = [];
        foreach (BuildingStairs stairs in file.Stairs)
        {
            MakeStairs(file, stairs, holes);
        }

        foreach (BuildingElevator elevator in file.Elevators)
        {
            MakeElevator(file, elevator, holes);
        }

        foreach (BuildingStorey storey in file.Storeys)
        {
            holes.TryGetValue(storey.Level, out List<SolidMesh>? storeyHoles);
            MakeStorey(file, storey, storeyHoles ?? [], subtract);
        }

        MakeRoof(file);
    }

    /// <summary>The stairs' place in the building: their foot's middle on the floor they climb from, turned the way they climb.</summary>
    public static Transform GetStairsPlace(BuildingFile file, BuildingStairs stairs)
    {
        Quaternion turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, float.DegreesToRadians(stairs.Heading));
        return new Transform { Position = new Vector3(stairs.X, file.GetFloorHeight(stairs.Level), stairs.Z), Rotation = turn };
    }

    /// <summary>The height the stairs climb, from their floor's top to the next one's; the file's storey height when there is none.</summary>
    public static float GetStairsHeight(BuildingFile file, BuildingStairs stairs)
    {
        return file.GetFloorHeight(stairs.Level + 1) - file.GetFloorHeight(stairs.Level);
    }

    public static StairsLayout GetStairsLayout(BuildingFile file, BuildingStairs stairs)
    {
        float upperFloor = file.FindStorey(stairs.Level + 1)?.Floor ?? file.Floor;
        return StairsLayout.Create(stairs, GetStairsHeight(file, stairs), upperFloor);
    }

    private static void MakeStairs(BuildingFile file, BuildingStairs stairs, Dictionary<int, List<SolidMesh>> holes)
    {
        StairsLayout layout = GetStairsLayout(file, stairs);
        Transform place = GetStairsPlace(file, stairs);
        stairs.Part = new SolidPart { Place = place, Mesh = layout.CreateSolid() };
        stairs.RailingPart = null;
        if (file.FindStorey(stairs.Level + 1) is null)
        {
            return;
        }

        float climb = GetStairsHeight(file, stairs);
        Matrix4x4 toBuilding = place.ToMatrix();
        float upperBase = file.GetBase(stairs.Level + 1) - file.GetFloorHeight(stairs.Level);
        foreach ((Vector2 low, Vector2 high) in layout.Openings)
        {
            SolidMesh box = StairsLayout.Box(low, high, upperBase - HoleReach, climb + HoleReach);
            Add(holes, stairs.Level + 1, box.Transform(toBuilding));
        }

        if (stairs.Railing && layout.CreateRailing(climb) is { } railing)
        {
            stairs.RailingPart = new SolidPart { Place = place, Mesh = railing };
        }
    }

    /// <summary>The storeys an elevator stops at, held to the building's: its lowest and highest.</summary>
    public static (int From, int To) GetElevatorSpan(BuildingFile file, BuildingElevator elevator)
    {
        int lowest = file.Storeys[0].Level;
        int highest = file.Storeys[^1].Level;
        int from = Math.Clamp(Math.Min(elevator.From, elevator.To), lowest, highest);
        int to = Math.Clamp(Math.Max(elevator.From, elevator.To), lowest, highest);
        return (from, to);
    }

    private static void MakeElevator(BuildingFile file, BuildingElevator elevator, Dictionary<int, List<SolidMesh>> holes)
    {
        (int from, int to) = GetElevatorSpan(file, elevator);
        elevator.From = from;
        elevator.To = to;
        Transform rest = new() { Position = new Vector3(elevator.X, file.GetFloorHeight(from), elevator.Z) };
        SolidMesh slab = SolidMesh.Box(new Vector3(elevator.Size, PlatformThickness, elevator.Size));
        elevator.Platform = new SolidPart { Place = rest, Mesh = slab };
        float half = (elevator.Size / 2) + ShaftClearance;
        Vector2 middle = new(elevator.X, elevator.Z);
        List<SolidMesh> rails = [];
        float turn = float.DegreesToRadians(elevator.Heading);
        Vector2 front = new(MathF.Sin(turn), -MathF.Cos(turn));
        for (int level = from + 1; level <= to; level++)
        {
            if (file.FindStorey(level) is null)
            {
                continue;
            }

            float bottom = file.GetBase(level) - HoleReach;
            float top = file.GetFloorHeight(level) + HoleReach;
            Add(holes, level, StairsLayout.Box(middle - new Vector2(half), middle + new Vector2(half), bottom, top));
            if (!elevator.Railing)
            {
                continue;
            }

            List<(Vector2, Vector2)> runs = GetShaftRails(middle, half, front);
            if (StairsLayout.CreateRails(runs, file.GetFloorHeight(level)) is { } railing)
            {
                rails.Add(railing);
            }
        }

        elevator.RailingPart = null;
        if (rails.Count > 0)
        {
            elevator.RailingPart = new SolidPart { Mesh = OpeningShape.Join(rails) };
        }
    }

    // A square round the shaft, but for the side it is stepped onto.
    private static List<(Vector2 From, Vector2 To)> GetShaftRails(Vector2 middle, float half, Vector2 front)
    {
        Vector2[] corners = [middle + new Vector2(-half, -half), middle + new Vector2(half, -half), middle + new Vector2(half, half), middle + new Vector2(-half, half)];
        List<(Vector2, Vector2)> runs = [];
        for (int index = 0; index < corners.Length; index++)
        {
            Vector2 from = corners[index];
            Vector2 to = corners[(index + 1) % corners.Length];
            Vector2 side = Vector2.Normalize(((from + to) / 2) - middle);
            if (Vector2.Dot(side, front) > 0.7f)
            {
                continue;
            }

            runs.Add((from, to));
        }

        return runs;
    }

    private static void Add(Dictionary<int, List<SolidMesh>> holes, int level, SolidMesh box)
    {
        if (!holes.TryGetValue(level, out List<SolidMesh>? list))
        {
            list = [];
            holes[level] = list;
        }

        list.Add(box);
    }

    private static void MakeStorey(BuildingFile file, BuildingStorey storey, List<SolidMesh> holes, Func<SolidMesh, SolidMesh, SolidMesh?>? subtract)
    {
        Dictionary<string, SolidPart> old = storey.Parts;
        Dictionary<string, SolidPart> parts = [];
        if (storey.Corners.Count == 0)
        {
            storey.Parts = parts;
            return;
        }

        storey.Shell = null;
        float bottom = file.GetBase(storey.Level);
        float floorTop = file.GetFloorHeight(storey.Level);
        Transform floorPlace = new() { Position = new Vector3(0, bottom, 0) };
        Transform wallPlace = new() { Position = new Vector3(0, floorTop, 0) };

        List<Vector2> outline = PlanGeometry.GetOutline(storey);
        if (outline.Count >= 3)
        {
            SolidMesh floor = Polygons.Prism(outline, 0, storey.Floor);
            foreach (PlanOpening hole in storey.Openings.Where(static opening => opening.Kind == OpeningKind.Floor))
            {
                Vector2 middle = new(hole.X, hole.Z);
                Vector2 size = new(hole.Width / 2, hole.Height / 2);
                holes.Add(StairsLayout.Box(middle - size, middle + size, bottom - HoleReach, floorTop + HoleReach));
            }

            floor = Cut(floor, holes, floorPlace, subtract);
            Put(parts, old, storey, "floor", floorPlace, floor);
        }

        List<SolidMesh> outside = [];
        foreach (PlanWall wall in storey.Walls)
        {
            float height = PlanGeometry.GetHeight(storey, wall);
            if (PlanGeometry.CreateWall(storey, wall, 0, height) is not { } solid)
            {
                continue;
            }

            List<SolidMesh> cutters = [];
            foreach (PlanOpening opening in storey.Openings)
            {
                if (opening.Kind != OpeningKind.Floor && opening.Wall == wall.Id && OpeningShape.Fits(file, storey, opening) && OpeningShape.TryGetFrame(file, storey, opening, out OpeningFrame frame))
                {
                    cutters.Add(OpeningShape.MakeCutter(frame, opening));
                }
            }

            solid = Cut(solid, cutters, wallPlace, subtract);
            if (wall.Outside)
            {
                outside.Add(solid);
            }
            else
            {
                Put(parts, old, storey, $"wall.{wall.Id}", wallPlace, solid);
            }
        }

        if (outside.Count > 0)
        {
            Put(parts, old, storey, "walls", wallPlace, OpeningShape.Join(outside));
        }

        float pillarHeight = storey.Height - storey.Floor;
        foreach (PlanPillar pillar in storey.Pillars)
        {
            Transform place = new() { Position = new Vector3(pillar.X, floorTop, pillar.Z) };
            Put(parts, old, storey, $"pillar.{pillar.Id}", place, MakePillar(pillar.Size, pillarHeight, pillar.Round));
        }

        if (storey.CornerPillars)
        {
            foreach (PlanCorner corner in storey.Corners)
            {
                if (!IsOutsideCorner(storey, corner))
                {
                    continue;
                }

                Transform place = new() { Position = new Vector3(corner.X, floorTop, corner.Z) };
                Put(parts, old, storey, $"corner.{corner.Id}", place, MakePillar(storey.CornerPillarSize, pillarHeight, false));
            }
        }

        foreach (PlanOpening opening in storey.Openings)
        {
            MakeOpening(file, storey, opening, parts, old, wallPlace);
        }

        storey.Parts = parts;
    }

    /// <summary>Whether outside walls turn at the corner: two of them meet there and do not run straight on.</summary>
    private static bool IsOutsideCorner(BuildingStorey storey, PlanCorner corner)
    {
        Vector2 point = PlanGeometry.GetPoint(corner);
        List<Vector2> ways = [];
        foreach (PlanWall wall in PlanGeometry.GetWallsAt(storey, corner.Id))
        {
            if (!wall.Outside)
            {
                continue;
            }

            int otherId = wall.From == corner.Id ? wall.To : wall.From;
            PlanCorner other = PlanGeometry.FindCorner(storey, otherId)!;
            Vector2 away = PlanGeometry.GetPoint(other) - point;
            ways.Add(Vector2.Normalize(away));
        }

        if (ways.Count != 2)
        {
            return ways.Count > 2;
        }

        float straight = Vector2.Dot(ways[0], ways[1]);
        return straight > -0.99f;
    }

        private static void MakeOpening(BuildingFile file, BuildingStorey storey, PlanOpening opening, Dictionary<string, SolidPart> parts, Dictionary<string, SolidPart> old, in Transform wallPlace)
    {
        if (opening.Kind == OpeningKind.Floor || !OpeningShape.Fits(file, storey, opening) || !OpeningShape.TryGetFrame(file, storey, opening, out OpeningFrame frame))
        {
            return;
        }

        if (opening.Kind == OpeningKind.Door)
        {
            DoorLeaf leaf = OpeningShape.MakeLeaf(frame, opening);
            string id = $"door.{opening.Id}";
            if (storey.IsFree(id) && old.TryGetValue(id, out SolidPart? kept))
            {
                parts[id] = kept;
            }
            else
            {
                Transform place = new() { Position = leaf.Pivot };
                PartJoint joint = new() { Kind = JointKind.Hinge, Pivot = leaf.Pivot, Axis = leaf.Axis, Min = 0, Max = Math.Clamp(opening.Angle, 1, 179), Speed = DoorSpeed };
                parts[id] = new SolidPart { Place = place, Mesh = leaf.Mesh, Joint = joint, Toggle = DoorAction };
            }
        }

        if (opening.Kind == OpeningKind.Window && opening.Glass)
        {
            Put(parts, old, storey, $"glass.{opening.Id}", wallPlace, ToPart(OpeningShape.MakeGlass(frame, opening), wallPlace));
        }

        if (opening.Frame > 0.005f)
        {
            Put(parts, old, storey, $"frame.{opening.Id}", wallPlace, ToPart(OpeningShape.MakeTrim(frame, opening), wallPlace));
        }
    }

    /// <summary>A square or round pillar standing on its origin.</summary>
    public static SolidMesh MakePillar(float size, float height, bool round)
    {
        if (round)
        {
            return SolidMesh.Cylinder(size / 2, height, RoundSides);
        }

        return SolidMesh.Box(new Vector3(size, height, size));
    }

    private static void MakeRoof(BuildingFile file)
    {
        BuildingRoof roof = file.Roof;
        BuildingStorey top = file.Storeys[^1];
        List<Vector2> outline = PlanGeometry.GetOutline(top);
        if (roof.None || outline.Count < 3)
        {
            roof.Part = null;
            return;
        }

        Transform place = new() { Position = new Vector3(0, file.GetTop(), 0) };
        List<float> reach = [.. outline.Select(_ => roof.Overhang)];
        List<Vector2> eaves = Polygons.Offset(Polygons.ToLeft(outline), reach);
        float thickness = Math.Max(roof.Thickness, 0.02f);
        if (roof.Kind == RoofKind.Flat)
        {
            roof.Part = new SolidPart { Place = place, Mesh = Polygons.Prism(eaves, 0, thickness) };
            return;
        }

        Vector2 low = eaves[0];
        Vector2 high = eaves[0];
        foreach (Vector2 point in eaves)
        {
            low = Vector2.Min(low, point);
            high = Vector2.Max(high, point);
        }

        // The slope's profile across the ridge, pushed along it: a gable's triangle, or a shed's wedge falling toward +across.
        float acrossLow = roof.AlongDepth ? low.X : low.Y;
        float acrossHigh = roof.AlongDepth ? high.X : high.Y;
        float alongLow = roof.AlongDepth ? low.Y : low.X;
        float alongHigh = roof.AlongDepth ? high.Y : high.X;
        float rise = Math.Max(roof.Rise, 0.05f);
        List<Vector2> profile;
        if (roof.Kind == RoofKind.Gable)
        {
            profile = [new(acrossLow, 0), new(acrossHigh, 0), new(acrossHigh, thickness), new((acrossLow + acrossHigh) / 2, rise + thickness), new(acrossLow, thickness)];
        }
        else
        {
            profile = [new(acrossLow, 0), new(acrossHigh, 0), new(acrossHigh, thickness), new(acrossLow, rise + thickness)];
        }

        SolidMesh prism = Polygons.Prism(profile, alongLow, alongHigh);

        // The prism's X is across the ridge, its height along it and its Z up.
        Matrix4x4 basis;
        if (roof.AlongDepth)
        {
            basis = new Matrix4x4(1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 1);
        }
        else
        {
            basis = new Matrix4x4(0, 0, 1, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1);
        }

        roof.Part = new SolidPart { Place = place, Mesh = prism.Transform(basis) };
    }

    private static SolidMesh Cut(SolidMesh solid, List<SolidMesh> cutters, in Transform place, Func<SolidMesh, SolidMesh, SolidMesh?>? subtract)
    {
        if (subtract is null || cutters.Count == 0)
        {
            return solid;
        }

        SolidMesh current = solid;
        foreach (SolidMesh cutter in cutters)
        {
            SolidMesh local = ToPart(cutter, place);
            if (subtract(current, local) is { } cut && cut.Faces.Count > 0)
            {
                current = cut;
            }
        }

        return current;
    }

    /// <summary>A solid in the building's space moved into the space of a part at the place (which only moves, never turns).</summary>
    private static SolidMesh ToPart(SolidMesh solid, in Transform place)
    {
        return solid.Transform(Matrix4x4.CreateTranslation(-place.Position));
    }

    private static void Put(Dictionary<string, SolidPart> parts, Dictionary<string, SolidPart> old, BuildingStorey storey, string id, in Transform place, SolidMesh mesh)
    {
        if (storey.IsFree(id) && old.TryGetValue(id, out SolidPart? kept))
        {
            parts[id] = kept;
            return;
        }

        parts[id] = new SolidPart { Place = place, Mesh = mesh };
    }
}
