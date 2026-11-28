using System.Numerics;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Buildings.Plans;

/// <summary>
/// Where an opening stands on its wall and the shapes it makes: its outline (flat or arched), the box it cuts from the wall, a door's
/// leaf with its hinge, and a window's pane, all in the building's space.
/// </summary>
public static class OpeningShape
{
    private const int ArchSegments = 12;

    /// <summary>How far past the wall's faces an opening's cut reaches, so it cuts clean through.</summary>
    private const float CutReach = 0.1f;

    /// <summary>How close to a wall's end, or its top, an opening may come.</summary>
    public const float EdgeClearance = 0.05f;

    /// <summary>What an opening may be short of its clearance by and still fit, so one held at a wall's end by the editor fits.</summary>
    private const float FitTolerance = 1e-4f;

    public const float GlassThickness = 0.02f;

    /// <summary>The least a leaf keeps round its window, and the smallest window it takes.</summary>
    public const float LeafRim = 0.05f;

    /// <summary>
    /// The opening's place on its wall in the building's space: the foot of its middle on the wall's centre line, the way along the
    /// wall, the way out of its right side, and the wall's thickness, length and height; false for a hole in the floor or a missing wall.
    /// </summary>
    public static bool TryGetFrame(BuildingFile file, BuildingStorey storey, PlanOpening opening, out OpeningFrame frame)
    {
        frame = default;
        if (opening.Kind == OpeningKind.Floor || PlanGeometry.FindWall(storey, opening.Wall) is not { } wall)
        {
            return false;
        }

        List<Vector2> path = PlanGeometry.GetPath(storey, wall);
        if (path.Count < 2)
        {
            return false;
        }

        float length = PlanGeometry.GetLength(path);
        (Vector2 point, Vector2 direction) = PlanGeometry.GetAlong(path, opening.Along);
        Vector2 outward = -Polygons.Left(direction);
        float floor = file.GetFloorHeight(storey.Level);
        Vector3 origin = new(point.X, floor + opening.Lift, point.Y);
        Vector3 along = new(direction.X, 0, direction.Y);
        Vector3 outside = new(outward.X, 0, outward.Y);
        frame = new OpeningFrame(origin, along, outside, PlanGeometry.GetThickness(storey, wall), length, PlanGeometry.GetHeight(storey, wall));
        return true;
    }

    /// <summary>Whether the opening lies wholly on its wall: clear of its ends and under its top; a hole must lie inside the floor.</summary>
    public static bool Fits(BuildingFile file, BuildingStorey storey, PlanOpening opening)
    {
        if (opening.Kind == OpeningKind.Floor)
        {
            List<Vector2> outline = PlanGeometry.GetOutline(storey);
            Vector2 centre = new(opening.X, opening.Z);
            return outline.Count >= 3 && Polygons.Contains(outline, centre);
        }

        if (!TryGetFrame(file, storey, opening, out OpeningFrame frame))
        {
            return false;
        }

        float half = (opening.Width / 2) + opening.Margin;
        float clearance = EdgeClearance - FitTolerance;
        bool along = opening.Along - half >= clearance && opening.Along + half <= frame.WallLength - clearance;
        bool up = opening.Lift >= 0 && opening.Lift + opening.Height + opening.Margin <= frame.WallHeight - clearance;
        return along && up && opening.Width > 0.05f && opening.Height > 0.05f;
    }

    /// <summary>The opening's outline across its wall, grown on every side but the foot by <paramref name="grow"/>: along, then up.</summary>
    public static List<Vector2> GetProfile(PlanOpening opening, float grow)
    {
        float half = (opening.Width / 2) + grow;
        float height = opening.Height + grow;
        float rise = 0;
        if (opening.Top == OpeningTop.Arch)
        {
            rise = Math.Clamp(opening.ArchRise, 0.01f, Math.Max(opening.Height - 0.05f, 0.01f));
        }

        float sides = height - rise;
        float bottom = -grow;
        List<Vector2> points = [new(-half, bottom), new(half, bottom), new(half, sides)];
        if (rise > 0)
        {
            for (int step = 1; step < ArchSegments; step++)
            {
                float angle = MathF.PI * step / ArchSegments;
                points.Add(new Vector2(half * MathF.Cos(angle), sides + (rise * MathF.Sin(angle))));
            }
        }

        points.Add(new Vector2(-half, sides));
        return points;
    }

    /// <summary>The solid the opening cuts out of its wall: its outline grown by the margin, through the wall and a little past it.</summary>
    public static SolidMesh MakeCutter(in OpeningFrame frame, PlanOpening opening)
    {
        List<Vector2> profile = GetProfile(opening, opening.Margin);
        float reach = (frame.WallThickness / 2) + CutReach;
        return Extrude(profile, -reach, reach, frame);
    }

    /// <summary>A window's pane of glass, in the middle of the wall.</summary>
    public static SolidMesh MakeGlass(in OpeningFrame frame, PlanOpening opening)
    {
        List<Vector2> profile = GetProfile(opening, 0);
        return Extrude(profile, -GlassThickness / 2, GlassThickness / 2, frame);
    }

    /// <summary>The frame round the opening: a post at each side and a lintel (or the arch's pieces) over it, standing out of the wall a little.</summary>
    public static SolidMesh MakeTrim(in OpeningFrame frame, PlanOpening opening)
    {
        List<Vector2> outer = GetProfile(opening, opening.Margin + opening.Frame);
        List<Vector2> inner = GetProfile(opening, opening.Margin);
        float reach = (frame.WallThickness / 2) + 0.03f;
        List<SolidMesh> pieces = [];

        // Each edge of the outline but the foot is a band between the inner and the outer outline.
        for (int index = 1; index < inner.Count; index++)
        {
            int next = (index + 1) % inner.Count;
            List<Vector2> band = [inner[index], inner[next], outer[next], outer[index]];
            if (Polygons.Area(band) < 1e-5f)
            {
                continue;
            }

            pieces.Add(Extrude(band, -reach, reach, frame));
        }

        return Join(pieces);
    }

    /// <summary>The first (or only) leaf of a door; see <see cref="MakeLeaves"/>.</summary>
    public static DoorLeaf MakeLeaf(in OpeningFrame frame, PlanOpening opening) => MakeLeaves(frame, opening, null)[0];

    /// <summary>
    /// A door's leaves and their hinges, one or two that meet in the middle: each leaf's solid in the space of its pivot, the pivot and
    /// the hinge's axis in the building's space (turning it by up to the opening's angle swings it to the side it opens to), and the
    /// leaf's middle, handle and free edge from the pivot. A leaf with a window has it cut out by <paramref name="subtract"/> (left
    /// whole without one) and, with glass, a pane in it.
    /// </summary>
    public static List<DoorLeaf> MakeLeaves(in OpeningFrame frame, PlanOpening opening, Func<SolidMesh, SolidMesh, SolidMesh?>? subtract)
    {
        float thickness = Math.Clamp(opening.Thickness, 0.01f, frame.WallThickness);
        float face = frame.WallThickness / 2;
        Vector3 swing = frame.Out;
        float near = face - thickness;
        float far = face;
        if (!opening.Outward)
        {
            swing = -frame.Out;
            near = -face;
            far = -face + thickness;
        }

        // Facing the door from the side it swings to, the hinge is on that person's left or right.
        Vector3 right = Vector3.Normalize(Vector3.Cross(-swing, Vector3.UnitY));
        Vector3 hingeSide = opening.HingeLeft ? -right : right;
        float side = Vector3.Dot(hingeSide, frame.Along) >= 0 ? 1 : -1;
        float half = opening.Width / 2;
        LeafCut cut = new(near, far, swing, subtract);
        if (!opening.Double)
        {
            return [MakeLeaf(frame, opening, side * half, -side * half, cut)];
        }

        return [MakeLeaf(frame, opening, side * half, 0, cut), MakeLeaf(frame, opening, -side * half, 0, cut)];
    }

    /// <summary>A leaf hinged at <paramref name="hinge"/> along the wall from the opening's middle, reaching to <paramref name="free"/>.</summary>
    private static DoorLeaf MakeLeaf(in OpeningFrame frame, PlanOpening opening, float hinge, float free, LeafCut cut)
    {
        float swingFace = opening.Outward ? cut.Far : cut.Near;
        Vector3 pivot = frame.Origin + (frame.Along * hinge) + (frame.Out * swingFace);
        Vector3 reach = frame.Along * (free - hinge);
        Vector3 turn = Vector3.Cross(Vector3.UnitY, reach);
        float sign = Vector3.Dot(turn, cut.Swing) >= 0 ? 1 : -1;
        Vector3 axis = Vector3.UnitY * sign;

        float low = Math.Min(hinge, free);
        float high = Math.Max(hinge, free);
        List<Vector2> whole = GetProfile(opening, 0);
        List<Vector2> profile = Polygons.ClipAcross(whole, low, high);
        SolidMesh leaf = Extrude(profile, cut.Near, cut.Far, frame);
        float middlePlane = (cut.Near + cut.Far) / 2;
        SolidMesh? glass = null;
        if (GetLeafWindow(opening, low, high) is { } window && cut.Subtract is { } subtract)
        {
            SolidMesh hole = Extrude(window, cut.Near - CutReach, cut.Far + CutReach, frame);
            if (subtract(leaf, hole) is { Faces.Count: > 0 } opened)
            {
                leaf = opened;
                if (opening.Glass)
                {
                    glass = Extrude(window, middlePlane - (GlassThickness / 2), middlePlane + (GlassThickness / 2), frame);
                }
            }
        }

        Matrix4x4 toPivot = Matrix4x4.CreateTranslation(-pivot);
        float inward = MathF.Sign(hinge - free);
        float inset = Math.Clamp(opening.HandleInset, 0, MathF.Abs(free - hinge));
        float handleAlong = free + (inward * inset);
        float handleUp = Math.Clamp(opening.HandleHeight, 0, opening.Height);
        Vector3 across = frame.Out * middlePlane;
        Vector3 middle = frame.Origin + (frame.Along * ((low + high) / 2)) + (Vector3.UnitY * (opening.Height / 2)) + across - pivot;
        Vector3 handle = frame.Origin + (frame.Along * handleAlong) + (Vector3.UnitY * handleUp) + across - pivot;
        Vector3 tip = frame.Origin + (frame.Along * free) + (frame.Out * swingFace) - pivot;
        return new DoorLeaf(leaf.Transform(toPivot), pivot, axis, middle, handle, tip, glass?.Transform(toPivot));
    }

    /// <summary>
    /// The window in a leaf between two distances along the wall, as its outline (along, up): centred across the leaf, held inside
    /// it with a rim of the leaf round it; null when the door has none or the leaf is too small for one.
    /// </summary>
    public static List<Vector2>? GetLeafWindow(PlanOpening opening, float low, float high)
    {
        if (opening.Kind != OpeningKind.Door || opening.WindowWidth <= 0)
        {
            return null;
        }

        float sides = opening.Height;
        if (opening.Top == OpeningTop.Arch)
        {
            sides -= Math.Clamp(opening.ArchRise, 0.01f, Math.Max(opening.Height - 0.05f, 0.01f));
        }

        float width = Math.Min(opening.WindowWidth, high - low - (2 * LeafRim));
        float from = Math.Clamp(opening.WindowFrom, LeafRim, Math.Max(sides - LeafRim, LeafRim));
        float height = Math.Min(opening.WindowHeight, sides - LeafRim - from);
        if (width < LeafRim || height < LeafRim)
        {
            return null;
        }

        float middle = (low + high) / 2;
        return [new(middle - (width / 2), from), new(middle + (width / 2), from), new(middle + (width / 2), from + height), new(middle - (width / 2), from + height)];
    }

    /// <summary>
    /// The opening's outline (along, up) pushed through the wall between two distances out of its right side, in the building's space.
    /// </summary>
    public static SolidMesh Extrude(IReadOnlyList<Vector2> profile, float near, float far, in OpeningFrame frame)
    {
        SolidMesh prism = Polygons.Prism(profile, near, far);

        // The prism's X is along the wall, its height out of it, and its Z up.
        Matrix4x4 basis = new(
            frame.Along.X, frame.Along.Y, frame.Along.Z, 0,
            frame.Out.X, frame.Out.Y, frame.Out.Z, 0,
            0, 1, 0, 0,
            frame.Origin.X, frame.Origin.Y, frame.Origin.Z, 1);
        return prism.Transform(basis);
    }

    /// <summary>Solids side by side as one, each still closed on its own.</summary>
    public static SolidMesh Join(IReadOnlyList<SolidMesh> solids)
    {
        List<Vector3> points = [];
        List<int[]> faces = [];
        foreach (SolidMesh solid in solids)
        {
            int first = points.Count;
            points.AddRange(solid.Points);
            foreach (IReadOnlyList<int> face in solid.Faces)
            {
                faces.Add([.. face.Select(index => index + first)]);
            }
        }

        return new SolidMesh(points, faces);
    }

    /// <summary>How a leaf is cut from its wall: between two distances out of the wall's right side, swinging one way, with what cuts its window.</summary>
    private readonly record struct LeafCut(float Near, float Far, Vector3 Swing, Func<SolidMesh, SolidMesh, SolidMesh?>? Subtract);
}
