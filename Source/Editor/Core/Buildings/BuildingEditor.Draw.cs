using System.Numerics;

using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Buildings;

public sealed partial class BuildingEditor
{
    private const int ArcSegments = 20;
    private const float Lifted = 0.02f;

    private static readonly Vector4 LineColor = new(0.85f, 0.78f, 0.66f, 0.6f);
    private static readonly Vector4 FaintColor = new(0.85f, 0.78f, 0.66f, 0.3f);
    private static readonly Vector4 SlabFill = new(0.85f, 0.78f, 0.66f, 0.07f);
    private static readonly Vector4 PickColor = new(1, 0.5f, 0.07f, 1);
    private static readonly Vector4 PickFill = new(1, 0.45f, 0.05f, 0.4f);
    private static readonly Vector4 HoverColor = new(1, 0.6f, 0.2f, 0.75f);
    private static readonly Vector4 HoverFill = new(1, 0.5f, 0.1f, 0.12f);
    private static readonly Vector4 CutColor = new(0.9f, 0.16f, 0.1f, 0.95f);
    private static readonly Vector4 CutFill = new(0.9f, 0.14f, 0.08f, 0.6f);
    private static readonly Vector4 GreenColor = new(0.3f, 0.75f, 0.2f, 1);
    private static readonly Vector4 GlassColor = new(0.35f, 0.55f, 0.75f, 0.9f);
    private static readonly Vector4 GlassFill = new(0.3f, 0.5f, 0.7f, 0.22f);
    private static readonly Vector4 HandleFill = new(1, 1, 1, 1);
    private static readonly Vector4 HandleLine = new(0.02f, 0.015f, 0.01f, 1);
    private static readonly Vector4 TriggerColor = new(1, 0.5f, 0.07f, 0.85f);
    private static readonly Vector4 TriggerFill = new(1, 0.45f, 0.05f, 0.18f);

    /// <summary>What a frame's drawing works on: the overlay, the building and the storey being edited, its place in the world and its floor's height.</summary>
    private readonly record struct Scene(OverlayBuilder Overlay, BuildingFile File, BuildingStorey Storey, Matrix4x4 Place, float Floor);

    public void Draw(OverlayBuilder overlay)
    {
        if (_file() is not { } file || file.FindStorey(_level) is not { } storey)
        {
            return;
        }

        Scene scene = new(overlay, file, storey, _place(), file.GetFloorHeight(_level));
        if (ShowsOpenStorey)
        {
            DrawOpenStorey(scene);
        }
        else if (_tool is BuildingTool.MoveCorner or BuildingTool.Wall or BuildingTool.Room or BuildingTool.SplitWall)
        {
            DrawCorners(scene);
        }

        DrawGhost(scene);
        if (_tool == BuildingTool.Opening)
        {
            foreach (PlanOpening opening in storey.Openings)
            {
                if (Picked is not { Kind: PlanPickKind.Opening or PlanPickKind.Handle } held || held.Id != opening.Id)
                {
                    DrawOpening(scene, opening, false, false);
                }
            }
        }

        if (Hover != Picked)
        {
            DrawPick(scene, Hover, true);
        }

        DrawPick(scene, Picked, false);
        DrawDrag(scene);
        if (_tool == BuildingTool.Camera || Picked is { Kind: PlanPickKind.Shot })
        {
            DrawShots(scene);
        }

        DrawGizmo(overlay);
    }

    private Vector3 ToWorld(in Scene scene, Vector3 point) => Vector3.Transform(point, scene.Place);

    private Vector3 ToWorld(in Scene scene, Vector2 point, float height) => Vector3.Transform(new Vector3(point.X, height, point.Y), scene.Place);

    /// <summary>Whether a face at a point of the building, looking out along a normal, is turned toward the eye.</summary>
    private bool Faces(in Matrix4x4 toWorld, Vector3 point, Vector3 normal)
    {
        Vector3 world = Vector3.Transform(point, toWorld);
        Vector3 out3 = Vector3.TransformNormal(normal, toWorld);
        Vector3 toEye = _camera.GetDirectionToEye(world);
        return Vector3.Dot(out3, toEye) > 0;
    }

    private static void AddLoop(in Scene scene, ReadOnlySpan<Vector3> loop, in Matrix4x4 toWorld, Vector4 color, float width, bool dashed = false)
    {
        Span<Vector3> world = stackalloc Vector3[loop.Length];
        for (int index = 0; index < loop.Length; index++)
        {
            world[index] = Vector3.Transform(loop[index], toWorld);
        }

        scene.Overlay.AddLoop(world, color, width, dashed);
    }

    private static void AddFill(in Scene scene, ReadOnlySpan<Vector3> corners, in Matrix4x4 toWorld, Vector4 color, bool hatched = false)
    {
        Span<Vector3> world = stackalloc Vector3[corners.Length];
        for (int index = 0; index < corners.Length; index++)
        {
            world[index] = Vector3.Transform(corners[index], toWorld);
        }

        scene.Overlay.AddFill(world, color, hatched);
    }

    private static void AddLine(in Scene scene, Vector3 from, Vector3 to, in Matrix4x4 toWorld, Vector4 color, float width, bool dashed = false)
    {
        Vector3 first = Vector3.Transform(from, toWorld);
        Vector3 second = Vector3.Transform(to, toWorld);
        if (dashed)
        {
            scene.Overlay.AddDashedLine(first, second, color, width);
        }
        else
        {
            scene.Overlay.AddLine(first, second, color, width);
        }
    }

    /// <summary>A flat shape of any outline across the floor at a height, filled.</summary>
    private static void FillPolygon(in Scene scene, IReadOnlyList<Vector2> polygon, float height, Vector4 color, bool hatched = false)
    {
        if (polygon.Count < 3)
        {
            return;
        }

        foreach ((int a, int b, int c) in Polygons.Triangulate(polygon))
        {
            Vector3 first = Vector3.Transform(new Vector3(polygon[a].X, height, polygon[a].Y), scene.Place);
            Vector3 second = Vector3.Transform(new Vector3(polygon[b].X, height, polygon[b].Y), scene.Place);
            Vector3 third = Vector3.Transform(new Vector3(polygon[c].X, height, polygon[c].Y), scene.Place);
            if (hatched)
            {
                scene.Overlay.AddHatch(first, second, third, color);
            }
            else
            {
                scene.Overlay.AddTriangle(first, second, third, color);
            }
        }
    }

    private static void OutlinePolygon(in Scene scene, IReadOnlyList<Vector2> polygon, float height, Vector4 color, float width, bool dashed = false)
    {
        Span<Vector3> loop = stackalloc Vector3[polygon.Count];
        for (int index = 0; index < polygon.Count; index++)
        {
            loop[index] = new Vector3(polygon[index].X, height, polygon[index].Y);
        }

        AddLoop(scene, loop, scene.Place, color, width, dashed);
    }

    /// <summary>
    /// An upright box in a frame (an origin, the way along and the way across, both level): the faces turned toward the eye filled, so
    /// the fill is even, and all its edges drawn.
    /// </summary>
    private void DrawBox(in Scene scene, in Matrix4x4 toWorld, Vector3 min, Vector3 max, Vector4 line, Vector4 fill, float width, bool dashed = false)
    {
        Span<Vector3> corners = stackalloc Vector3[8];
        for (int corner = 0; corner < 8; corner++)
        {
            float x = (corner & 1) == 0 ? min.X : max.X;
            float y = (corner & 2) == 0 ? min.Y : max.Y;
            float z = (corner & 4) == 0 ? min.Z : max.Z;
            corners[corner] = new Vector3(x, y, z);
        }

        if (fill.W > 0)
        {
            ReadOnlySpan<int> faces = [0, 1, 3, 2, 4, 6, 7, 5, 0, 4, 5, 1, 2, 3, 7, 6, 0, 2, 6, 4, 1, 5, 7, 3];
            ReadOnlySpan<Vector3> normals = [-Vector3.UnitZ, Vector3.UnitZ, -Vector3.UnitY, Vector3.UnitY, -Vector3.UnitX, Vector3.UnitX];
            Span<Vector3> quad = stackalloc Vector3[4];
            for (int face = 0; face < 6; face++)
            {
                for (int corner = 0; corner < 4; corner++)
                {
                    quad[corner] = corners[faces[(face * 4) + corner]];
                }

                Vector3 middle = (quad[0] + quad[2]) / 2;
                if (Faces(toWorld, middle, normals[face]))
                {
                    AddFill(scene, quad, toWorld, fill);
                }
            }
        }

        ReadOnlySpan<int> edges = [0, 1, 1, 3, 3, 2, 2, 0, 4, 5, 5, 7, 7, 6, 6, 4, 0, 4, 1, 5, 2, 6, 3, 7];
        for (int edge = 0; edge < edges.Length; edge += 2)
        {
            AddLine(scene, corners[edges[edge]], corners[edges[edge + 1]], toWorld, line, width, dashed);
        }
    }

    private void DrawCorners(in Scene scene)
    {
        float top = scene.Floor + scene.Storey.Height - scene.Storey.Floor;
        foreach (PlanCorner corner in scene.Storey.Corners)
        {
            if (Picked is { Kind: PlanPickKind.Corner } pick && pick.Id == corner.Id)
            {
                continue;
            }

            Vector3 head = ToWorld(scene, PlanGeometry.GetPoint(corner), top);
            scene.Overlay.AddHandle(head, 3.5f, HandleFill, HandleLine);
        }
    }

    private void DrawGhost(in Scene scene)
    {
        foreach (Vector3[] loop in _ghost)
        {
            AddLoop(scene, loop, scene.Place, LineColor, OverlayLines.Thin, true);
        }
    }

    private void DrawPick(in Scene scene, PlanPick? pick, bool hover)
    {
        if (pick is null || pick.Level != _level && pick.Kind is not (PlanPickKind.Elevator or PlanPickKind.Shot))
        {
            return;
        }

        Vector4 line = hover ? HoverColor : PickColor;
        Vector4 fill = hover ? HoverFill : PickFill;
        float width = hover ? OverlayLines.Thin : OverlayLines.Bold;
        BuildingStorey storey = scene.Storey;
        switch (pick.Kind)
        {
            case PlanPickKind.Wall when PlanGeometry.FindWall(storey, pick.Id) is { } wall:
                DrawWall(scene, wall, line, fill, width);
                if (!hover)
                {
                    DrawWallTools(scene, wall);
                }

                break;
            case PlanPickKind.Corner when PlanGeometry.FindCorner(storey, pick.Id) is { } corner:
                float top = scene.Floor + storey.Height - storey.Floor;
                Vector3 foot = ToWorld(scene, PlanGeometry.GetPoint(corner), scene.Floor);
                Vector3 head = ToWorld(scene, PlanGeometry.GetPoint(corner), top);
                scene.Overlay.AddLine(foot, head, line, width);
                scene.Overlay.AddHandle(foot, 4, line, HandleLine);
                scene.Overlay.AddHandle(head, 4, line, HandleLine);
                break;
            case PlanPickKind.Room:
                foreach (RoomShape room in PlanGeometry.FindRooms(storey))
                {
                    if (room.Record >= 0 && storey.Rooms[room.Record].Name == pick.Room)
                    {
                        FillPolygon(scene, room.Polygon, scene.Floor + Lifted, fill);
                        OutlinePolygon(scene, room.Polygon, scene.Floor + Lifted, line, width);
                    }
                }

                break;
            case PlanPickKind.Pillar when storey.Pillars.Find(pillar => pillar.Id == pick.Id) is { } pillar:
                float half = pillar.Size / 2;
                Vector3 low = new(pillar.X - half, scene.Floor, pillar.Z - half);
                Vector3 high = new(pillar.X + half, scene.Floor + storey.Height - storey.Floor, pillar.Z + half);
                DrawBox(scene, scene.Place, low, high, line, fill, width);
                break;
            case PlanPickKind.Stairs when scene.File.Stairs.Find(found => found.Id == pick.Id) is { } stairs:
                DrawStairs(scene, stairs, line, fill, width);
                if (!hover && _tool == BuildingTool.Stairs)
                {
                    DrawStairsTools(scene, stairs);
                }

                break;
            case PlanPickKind.Elevator when scene.File.Elevators.Find(found => found.Id == pick.Id) is { } elevator:
                DrawElevator(scene, elevator, line, fill, width);
                if (!hover && _tool == BuildingTool.Elevator)
                {
                    scene.Overlay.AddHandle(ToWorld(scene, GetElevatorHandle(scene.File, elevator)), 4, HandleFill, HandleLine);
                }

                break;
            case PlanPickKind.Opening or PlanPickKind.Handle when storey.Openings.Find(opening => opening.Id == pick.Id) is { } opening:
                DrawOpening(scene, opening, !hover, hover);
                break;
            default:
                break;
        }
    }

    /// <summary>A wall as it stands: its faces toward the eye filled and its edges drawn, an arc's stretches among them.</summary>
    private void DrawWall(in Scene scene, PlanWall wall, Vector4 line, Vector4 fill, float width)
    {
        BuildingStorey storey = scene.Storey;
        List<Vector2> path = PlanGeometry.GetPath(storey, wall);
        if (path.Count < 2)
        {
            return;
        }

        (List<Vector2> left, List<Vector2> right) = PlanGeometry.GetSides(storey, wall, path, PlanGeometry.GetThickness(storey, wall));
        int count = Math.Min(left.Count, right.Count);
        float bottom = scene.Floor;
        float top = scene.Floor + PlanGeometry.GetHeight(storey, wall);
        Span<Vector3> quad = stackalloc Vector3[4];
        for (int index = 1; index < count; index++)
        {
            Vector2 across = left[index - 1] - right[index - 1];
            Vector3 outLeft = Vector3.Normalize(new Vector3(across.X, 0, across.Y));
            FillWallFace(scene, left[index - 1], left[index], bottom, top, outLeft, fill, quad);
            FillWallFace(scene, right[index - 1], right[index], bottom, top, -outLeft, fill, quad);
            quad[0] = new Vector3(left[index - 1].X, top, left[index - 1].Y);
            quad[1] = new Vector3(left[index].X, top, left[index].Y);
            quad[2] = new Vector3(right[index].X, top, right[index].Y);
            quad[3] = new Vector3(right[index - 1].X, top, right[index - 1].Y);
            if (Faces(scene.Place, quad[0], Vector3.UnitY))
            {
                AddFill(scene, quad, scene.Place, fill);
            }

            AddLine(scene, quad[0], quad[1], scene.Place, line, width);
            AddLine(scene, quad[3], quad[2], scene.Place, line, width);
            Vector3 lowLeft = new(left[index - 1].X, bottom, left[index - 1].Y);
            Vector3 lowLeftNext = new(left[index].X, bottom, left[index].Y);
            Vector3 lowRight = new(right[index - 1].X, bottom, right[index - 1].Y);
            Vector3 lowRightNext = new(right[index].X, bottom, right[index].Y);
            AddLine(scene, lowLeft, lowLeftNext, scene.Place, line, width);
            AddLine(scene, lowRight, lowRightNext, scene.Place, line, width);
            if (index > 1)
            {
                AddLine(scene, lowLeft, quad[0], scene.Place, line, OverlayLines.Thin);
                AddLine(scene, lowRight, quad[3], scene.Place, line, OverlayLines.Thin);
            }
        }

        Vector2 direction = Vector2.Normalize(path[^1] - path[0]);
        Vector3 forward = new(direction.X, 0, direction.Y);
        DrawWallEnd(scene, left[0], right[0], bottom, top, -forward, line, fill, width);
        DrawWallEnd(scene, left[count - 1], right[count - 1], bottom, top, forward, line, fill, width);
    }

    private void FillWallFace(in Scene scene, Vector2 from, Vector2 to, float bottom, float top, Vector3 outward, Vector4 fill, Span<Vector3> quad)
    {
        quad[0] = new Vector3(from.X, bottom, from.Y);
        quad[1] = new Vector3(to.X, bottom, to.Y);
        quad[2] = new Vector3(to.X, top, to.Y);
        quad[3] = new Vector3(from.X, top, from.Y);
        if (Faces(scene.Place, (quad[0] + quad[2]) / 2, outward))
        {
            AddFill(scene, quad, scene.Place, fill);
        }
    }

    private void DrawWallEnd(in Scene scene, Vector2 left, Vector2 right, float bottom, float top, Vector3 outward, Vector4 line, Vector4 fill, float width)
    {
        Span<Vector3> quad =
        [
            new Vector3(left.X, bottom, left.Y),
            new Vector3(right.X, bottom, right.Y),
            new Vector3(right.X, top, right.Y),
            new Vector3(left.X, top, left.Y),
        ];
        if (Faces(scene.Place, (quad[0] + quad[2]) / 2, outward))
        {
            AddFill(scene, quad, scene.Place, fill);
        }

        AddLoop(scene, quad, scene.Place, line, width);
    }

    /// <summary>What the push and arc tools show on the picked wall: the arrow to drag it by, or its chord and the square that bends it.</summary>
    private void DrawWallTools(in Scene scene, PlanWall wall)
    {
        BuildingStorey storey = scene.Storey;
        if (_tool == BuildingTool.PushWall)
        {
            (Vector3 from, Vector3 tip) = GetPushArrow(storey, wall, scene.Floor);
            Vector3 neck = Vector3.Lerp(from, tip, 0.78f);
            scene.Overlay.AddLine(ToWorld(scene, from), ToWorld(scene, neck), PickColor, OverlayLines.Handle);
            scene.Overlay.AddCone(ToWorld(scene, neck), ToWorld(scene, tip), 6, PickColor);
            return;
        }

        if (_tool != BuildingTool.Arc || PlanGeometry.FindCorner(storey, wall.From) is not { } start || PlanGeometry.FindCorner(storey, wall.To) is not { } end)
        {
            return;
        }

        float top = scene.Floor + PlanGeometry.GetHeight(storey, wall);
        List<Vector2> path = PlanGeometry.GetPath(storey, wall);
        (Vector2 bent, _) = PlanGeometry.GetAlong(path, PlanGeometry.GetLength(path) / 2);
        Vector2 first = PlanGeometry.GetPoint(start);
        Vector2 last = PlanGeometry.GetPoint(end);
        Vector3 chordFrom = ToWorld(scene, first, top);
        Vector3 chordTo = ToWorld(scene, last, top);
        Vector3 chordMiddle = ToWorld(scene, (first + last) / 2, top);
        Vector3 handle = ToWorld(scene, bent, top);
        scene.Overlay.AddDashedLine(chordFrom, chordTo, LineColor, OverlayLines.Thin);
        scene.Overlay.AddLine(chordMiddle, handle, PickColor, OverlayLines.Bold);
        scene.Overlay.AddHandle(chordFrom, 3.5f, HandleFill, HandleLine);
        scene.Overlay.AddHandle(chordTo, 3.5f, HandleFill, HandleLine);
        scene.Overlay.AddHandle(handle, 4.5f, PickColor, HandleLine);
    }

    /// <summary>
    /// An opening on its wall: its outline on both faces, a window's pane, a door's hinges, swing, handle and leaf window; picked, it is
    /// filled, the cut round it is striped red, and the arrows that slide and lift it stand on it. Red where it no longer fits.
    /// </summary>
    private void DrawOpening(in Scene scene, PlanOpening opening, bool picked, bool hover)
    {
        BuildingFile file = scene.File;
        BuildingStorey storey = scene.Storey;
        Vector4 line = LineColor;
        float width = OverlayLines.Thin;
        if (hover)
        {
            line = HoverColor;
        }

        if (picked)
        {
            line = PickColor;
            width = OverlayLines.Bold;
        }

        bool fits = OpeningShape.Fits(file, storey, opening);
        if (!fits)
        {
            line = CutColor;
        }

        if (opening.Kind == OpeningKind.Floor)
        {
            if (GetOpeningLoop(file, storey, opening, 0, 0) is { } hole)
            {
                AddFill(scene, hole, scene.Place, picked ? CutFill : CutFill with { W = 0.35f }, true);
                AddLoop(scene, hole, scene.Place, picked ? PickColor : CutColor, width);
            }

            return;
        }

        if (!OpeningShape.TryGetFrame(file, storey, opening, out OpeningFrame frame))
        {
            return;
        }

        // The face of the wall the eye sees carries the fill; both carry the outline.
        float seen = Faces(scene.Place, frame.Origin, frame.Out) ? 1 : -1;
        Vector3[] near = GetOpeningLoop(file, storey, opening, 0, seen)!;
        Vector3[] far = GetOpeningLoop(file, storey, opening, 0, -seen)!;
        if (picked)
        {
            Vector3[] cut = GetOpeningLoop(file, storey, opening, Math.Max(opening.Margin, 0.07f), seen)!;
            DrawCutBand(scene, near, cut, frame.Origin.Y);
            AddFill(scene, near, scene.Place, fits ? PickFill : CutFill with { W = 0.3f });
        }
        else if (hover)
        {
            AddFill(scene, near, scene.Place, HoverFill);
        }

        if (opening.Kind == OpeningKind.Window && opening.Glass)
        {
            AddFill(scene, near, scene.Place, GlassFill);
        }

        AddLoop(scene, far, scene.Place, line with { W = line.W * 0.5f }, OverlayLines.Thin);
        AddLoop(scene, near, scene.Place, line, width);
        if (opening.Kind == OpeningKind.Door)
        {
            DrawDoor(scene, frame, opening, picked);
        }

        if (!picked)
        {
            return;
        }

        (Vector3 foot, Vector3 tip) = GetLiftArrow(frame);
        Vector3 neck = Vector3.Lerp(foot, tip, 0.78f);
        scene.Overlay.AddLine(ToWorld(scene, foot), ToWorld(scene, neck), GreenColor, OverlayLines.Handle);
        scene.Overlay.AddCone(ToWorld(scene, neck), ToWorld(scene, tip), 6, GreenColor);
        scene.Overlay.AddHandle(ToWorld(scene, foot), 4, GreenColor, HandleLine);
        if (_tool != BuildingTool.Opening)
        {
            return;
        }

        // The double arrow says it slides along its wall.
        float face = seen * ((frame.WallThickness / 2) + 0.01f);
        Vector3 middle = frame.Origin + (Vector3.UnitY * Math.Min(opening.Height / 2, 0.9f)) + (frame.Out * face);
        float reach = GetUnitsPerPixel(middle) * ArrowPixels * 0.8f;
        Vector3 one = middle - (frame.Along * reach);
        Vector3 other = middle + (frame.Along * reach);
        scene.Overlay.AddLine(ToWorld(scene, Vector3.Lerp(middle, one, 0.75f)), ToWorld(scene, Vector3.Lerp(middle, other, 0.75f)), PickColor, OverlayLines.Handle);
        scene.Overlay.AddCone(ToWorld(scene, Vector3.Lerp(middle, one, 0.75f)), ToWorld(scene, one), 5, PickColor);
        scene.Overlay.AddCone(ToWorld(scene, Vector3.Lerp(middle, other, 0.75f)), ToWorld(scene, other), 5, PickColor);
        scene.Overlay.AddDot(ToWorld(scene, middle), 3.5f, HandleFill);
    }

    /// <summary>The band between an opening and what is cut for it, striped red, but along its foot.</summary>
    private static void DrawCutBand(in Scene scene, Vector3[] inner, Vector3[] outer, float foot)
    {
        if (inner.Length != outer.Length)
        {
            return;
        }

        Span<Vector3> quad = stackalloc Vector3[4];
        for (int index = 0; index < inner.Length; index++)
        {
            int next = (index + 1) % inner.Length;
            if (MathF.Abs(inner[index].Y - foot) < 1e-3f && MathF.Abs(inner[next].Y - foot) < 1e-3f)
            {
                continue;
            }

            quad[0] = inner[index];
            quad[1] = inner[next];
            quad[2] = outer[next];
            quad[3] = outer[index];
            AddFill(scene, quad, scene.Place, CutFill, true);
        }

        AddLoop(scene, outer, scene.Place, CutColor, OverlayLines.Thin);
    }

    /// <summary>
    /// A door's leaves as they play: each hinge as a green line with its two hinges on it, the way the free edge swings as a green arc
    /// on the floor with the leaf standing open at its end, the handle as a dot where a hand takes it, and the window in the leaf.
    /// </summary>
    private void DrawDoor(in Scene scene, in OpeningFrame frame, PlanOpening opening, bool picked)
    {
        float width = picked ? OverlayLines.Bold : OverlayLines.Thin;
        Vector4 green = picked ? GreenColor : GreenColor with { W = 0.6f };
        bool handlePicked = Picked is { Kind: PlanPickKind.Handle } handle && handle.Id == opening.Id;
        foreach (DoorLeaf leaf in OpeningShape.MakeLeaves(frame, opening, null))
        {
            Vector3 pivot = leaf.Pivot;
            Vector3 up = Vector3.UnitY * opening.Height;
            AddLine(scene, pivot, pivot + up, scene.Place, green, width);
            scene.Overlay.AddDot(ToWorld(scene, pivot + (up * 0.12f)), 2.5f, green);
            scene.Overlay.AddDot(ToWorld(scene, pivot + (up * 0.88f)), 2.5f, green);
            Vector3 previous = pivot + leaf.Free + (Vector3.UnitY * Lifted);
            Vector3 open = previous;
            for (int step = 1; step <= ArcSegments; step++)
            {
                float angle = float.DegreesToRadians(opening.Angle) * step / ArcSegments;
                Quaternion turned = Quaternion.CreateFromAxisAngle(leaf.Axis, angle);
                open = pivot + Vector3.Transform(leaf.Free, turned) + (Vector3.UnitY * Lifted);
                AddLine(scene, previous, open, scene.Place, green, width);
                previous = open;
            }

            AddLine(scene, pivot + (Vector3.UnitY * Lifted), open, scene.Place, green, OverlayLines.Thin, true);
            if (picked)
            {
                AddLine(scene, open, open + up, scene.Place, green with { W = 0.5f }, OverlayLines.Thin, true);
                AddLine(scene, pivot + up, open + up, scene.Place, green with { W = 0.5f }, OverlayLines.Thin, true);
            }

            Vector3 grip = ToWorld(scene, pivot + leaf.Handle);
            scene.Overlay.AddDot(grip, handlePicked ? 6 : 5, HandleLine);
            scene.Overlay.AddDot(grip, handlePicked ? 4.5f : 3.5f, handlePicked ? PickColor : HandleFill);
        }

        DrawLeafWindows(scene, frame, opening, width);
    }

    private void DrawLeafWindows(in Scene scene, in OpeningFrame frame, PlanOpening opening, float width)
    {
        float half = opening.Width / 2;
        float seen = Faces(scene.Place, frame.Origin, frame.Out) ? 1 : -1;
        float face = seen * ((frame.WallThickness / 2) + 0.01f);
        (float Low, float High)[] leaves = opening.Double ? [(-half, 0), (0, half)] : [(-half, half)];
        foreach ((float low, float high) in leaves)
        {
            if (OpeningShape.GetLeafWindow(opening, low, high) is not { } window)
            {
                continue;
            }

            Vector3[] loop = new Vector3[window.Count];
            for (int index = 0; index < window.Count; index++)
            {
                loop[index] = frame.Origin + (frame.Along * window[index].X) + (Vector3.UnitY * window[index].Y) + (frame.Out * face);
            }

            if (opening.Glass)
            {
                AddFill(scene, loop, scene.Place, GlassFill);
            }

            AddLoop(scene, loop, scene.Place, GlassColor, width);
        }
    }

    /// <summary>Stairs as they stand: each step's top and the sides that show above the steps beside it, filled toward the eye and outlined.</summary>
    private void DrawStairs(in Scene scene, BuildingStairs stairs, Vector4 line, Vector4 fill, float width)
    {
        StairsLayout layout = BuildingMaker.GetStairsLayout(scene.File, stairs);
        Transform place = BuildingMaker.GetStairsPlace(scene.File, stairs);
        Matrix4x4 toWorld = place.ToMatrix() * scene.Place;
        List<(Vector2 Low, Vector2 High, float Top)> blocks = layout.Blocks;
        Vector4 side = fill with { W = fill.W * 0.75f };
        Span<Vector3> quad = stackalloc Vector3[4];
        for (int index = 0; index < blocks.Count; index++)
        {
            (Vector2 low, Vector2 high, float top) = blocks[index];
            quad[0] = new Vector3(low.X, top, low.Y);
            quad[1] = new Vector3(high.X, top, low.Y);
            quad[2] = new Vector3(high.X, top, high.Y);
            quad[3] = new Vector3(low.X, top, high.Y);
            if (Faces(toWorld, quad[0], Vector3.UnitY))
            {
                AddFill(scene, quad, toWorld, fill);
            }

            AddLoop(scene, quad, toWorld, line, width);
            DrawStepSide(scene, toWorld, blocks, index, new Vector2(low.X, low.Y), new Vector2(high.X, low.Y), -Vector3.UnitZ, line, side);
            DrawStepSide(scene, toWorld, blocks, index, new Vector2(high.X, high.Y), new Vector2(low.X, high.Y), Vector3.UnitZ, line, side);
            DrawStepSide(scene, toWorld, blocks, index, new Vector2(low.X, high.Y), new Vector2(low.X, low.Y), -Vector3.UnitX, line, side);
            DrawStepSide(scene, toWorld, blocks, index, new Vector2(high.X, low.Y), new Vector2(high.X, high.Y), Vector3.UnitX, line, side);
        }
    }

    /// <summary>A step's side from the top of the step beside it (the floor with none) up to its own top.</summary>
    private void DrawStepSide(in Scene scene, in Matrix4x4 toWorld, List<(Vector2 Low, Vector2 High, float Top)> blocks, int index, Vector2 from, Vector2 to, Vector3 outward, Vector4 line, Vector4 fill)
    {
        float top = blocks[index].Top;
        float bottom = GetNeighbourTop(blocks, index, from, to, outward);
        if (bottom >= top - 1e-3f)
        {
            return;
        }

        Span<Vector3> quad =
        [
            new Vector3(from.X, bottom, from.Y),
            new Vector3(to.X, bottom, to.Y),
            new Vector3(to.X, top, to.Y),
            new Vector3(from.X, top, from.Y),
        ];
        if (Faces(toWorld, (quad[0] + quad[2]) / 2, outward))
        {
            AddFill(scene, quad, toWorld, fill);
        }

        AddLine(scene, quad[0], quad[3], toWorld, line, OverlayLines.Thin);
        AddLine(scene, quad[1], quad[2], toWorld, line, OverlayLines.Thin);
        AddLine(scene, quad[0], quad[1], toWorld, line, OverlayLines.Thin);
    }

    /// <summary>How high the block that lies against a side of a block reaches there, most of the side's length; 0 with none.</summary>
    private static float GetNeighbourTop(List<(Vector2 Low, Vector2 High, float Top)> blocks, int index, Vector2 from, Vector2 to, Vector3 outward)
    {
        const float Near = 1e-3f;
        float found = 0;
        float most = Near;
        bool alongX = MathF.Abs(outward.Z) > 0.5f;
        for (int other = 0; other < blocks.Count; other++)
        {
            if (other == index)
            {
                continue;
            }

            (Vector2 low, Vector2 high, float top) = blocks[other];
            float shared;
            if (alongX)
            {
                float edge = outward.Z > 0 ? low.Y : high.Y;
                if (MathF.Abs(edge - from.Y) > Near)
                {
                    continue;
                }

                shared = Math.Min(Math.Max(from.X, to.X), high.X) - Math.Max(Math.Min(from.X, to.X), low.X);
            }
            else
            {
                float edge = outward.X > 0 ? low.X : high.X;
                if (MathF.Abs(edge - from.X) > Near)
                {
                    continue;
                }

                shared = Math.Min(Math.Max(from.Y, to.Y), high.Y) - Math.Max(Math.Min(from.Y, to.Y), low.Y);
            }

            if (shared > most)
            {
                most = shared;
                found = top;
            }
        }

        return found;
    }

    /// <summary>What the stairs tool shows on picked stairs: the green arrow at the foot that says which way they climb, and the squares that shape them.</summary>
    private void DrawStairsTools(in Scene scene, BuildingStairs stairs)
    {
        Transform place = BuildingMaker.GetStairsPlace(scene.File, stairs);
        Matrix4x4 toBuilding = place.ToMatrix();
        Vector3 foot = Vector3.Transform(new Vector3(0, Lifted, 0), toBuilding);
        Vector3 climbing = Vector3.Transform(Vector3.UnitZ, place.Rotation);
        float reach = GetUnitsPerPixel(foot) * ArrowPixels * 0.7f;
        Vector3 tail = foot - (climbing * reach);
        Vector3 neck = Vector3.Lerp(tail, foot, 0.7f);
        scene.Overlay.AddLine(ToWorld(scene, tail), ToWorld(scene, neck), GreenColor, OverlayLines.Handle);
        scene.Overlay.AddCone(ToWorld(scene, neck), ToWorld(scene, foot), 5, GreenColor);
        (Vector3 width, Vector3? run, Vector3 top) = GetStairsHandles(scene.File, stairs);
        scene.Overlay.AddHandle(ToWorld(scene, foot), 4, HandleFill, HandleLine);
        scene.Overlay.AddHandle(ToWorld(scene, width), 4, HandleFill, HandleLine);
        if (run is { } runHandle)
        {
            scene.Overlay.AddHandle(ToWorld(scene, runHandle), 4, HandleFill, HandleLine);
        }

        scene.Overlay.AddHandle(ToWorld(scene, top), 4.5f, PickColor, HandleLine);
    }

    /// <summary>
    /// An elevator through the storeys: its square on this floor, the shaft it passes through as dashed lines from its lowest stop to
    /// over its highest, its platform where it rests, and a green mark on the side it is stepped onto.
    /// </summary>
    private void DrawElevator(in Scene scene, BuildingElevator elevator, Vector4 line, Vector4 fill, float width)
    {
        BuildingFile file = scene.File;
        (int from, int to) = BuildingMaker.GetElevatorSpan(file, elevator);
        float half = elevator.Size / 2;
        Vector3[] here = GetElevatorSquare(file, elevator, _level, Lifted);
        AddFill(scene, here, scene.Place, fill);
        AddLoop(scene, here, scene.Place, line, width);
        float bottom = file.GetFloorHeight(from);
        float top = file.GetFloorHeight(to) + BuildingRegions.Headroom;
        Transform rest = BuildingMaker.GetElevatorPlace(file, elevator, from);
        Matrix4x4 toWorld = rest.ToMatrix() * scene.Place;
        Vector3 shaftLow = new(-half, 0, -half);
        Vector3 shaftHigh = new(half, top - bottom, half);
        DrawBox(scene, toWorld, shaftLow, shaftHigh, line with { W = line.W * 0.7f }, default, OverlayLines.Thin, true);
        foreach ((int stop, float height) in file.GetElevatorStops(elevator))
        {
            if (stop == from)
            {
                continue;
            }

            Span<Vector3> ring = [new(-half, height, -half), new(half, height, -half), new(half, height, half), new(-half, height, half)];
            AddLoop(scene, ring, toWorld, line with { W = line.W * 0.5f }, OverlayLines.Thin, true);
        }

        float inside = half - BuildingMaker.ShaftClearance;
        Vector3 platformLow = new(-inside, 0, -inside);
        Vector3 platformHigh = new(inside, BuildingMaker.PlatformThickness, inside);
        DrawBox(scene, toWorld, platformLow, platformHigh, line, fill with { W = fill.W * 0.6f }, OverlayLines.Thin);
        Transform level = BuildingMaker.GetElevatorPlace(file, elevator, _level);
        Matrix4x4 here3 = level.ToMatrix() * scene.Place;
        Vector3 edge = new(0, Lifted, -half);
        Vector3 outside = new(0, Lifted, -half - (GetUnitsPerPixel(level.Position) * ArrowPixels * 0.5f));
        Vector3 neck = Vector3.Lerp(outside, edge, 0.6f);
        scene.Overlay.AddLine(Vector3.Transform(outside, here3), Vector3.Transform(neck, here3), GreenColor, OverlayLines.Bold);
        scene.Overlay.AddCone(Vector3.Transform(neck, here3), Vector3.Transform(edge, here3), 4, GreenColor);
    }

    /// <summary>
    /// The storey shown open for the stairs and elevator tools: its walls as their lines on the floor, the floor above see-through over
    /// it, held up by thin lines at its corners, and striped red where stairs, shafts and holes are cut out of that floor.
    /// </summary>
    private void DrawOpenStorey(in Scene scene)
    {
        BuildingFile file = scene.File;
        BuildingStorey storey = scene.Storey;
        float floor = scene.Floor + Lifted;
        foreach (PlanWall wall in storey.Walls)
        {
            Vector3[] loop = GetWallLoop(storey, wall, floor);
            AddLoop(scene, loop, scene.Place, LineColor, OverlayLines.Thin);
        }

        foreach (BuildingStairs stairs in file.Stairs)
        {
            if (stairs.Level == _level && (Picked is not { Kind: PlanPickKind.Stairs } held || held.Id != stairs.Id))
            {
                DrawStairs(scene, stairs, LineColor, HoverFill, OverlayLines.Thin);
            }
        }

        foreach (BuildingElevator elevator in file.Elevators)
        {
            (int from, int to) = BuildingMaker.GetElevatorSpan(file, elevator);
            if (_level >= from && _level <= to && (Picked is not { Kind: PlanPickKind.Elevator } held || held.Id != elevator.Id))
            {
                DrawElevator(scene, elevator, LineColor, HoverFill, OverlayLines.Thin);
            }
        }

        if (file.FindStorey(_level + 1) is not { } above)
        {
            return;
        }

        float slabBottom = file.GetBase(_level + 1);
        float slabTop = file.GetFloorHeight(_level + 1);
        List<Vector2> outline = PlanGeometry.GetOutline(above);
        FillPolygon(scene, outline, slabTop, SlabFill);
        OutlinePolygon(scene, outline, slabTop, FaintColor, OverlayLines.Thin);
        OutlinePolygon(scene, outline, slabBottom, FaintColor, OverlayLines.Thin);
        foreach (Vector2 corner in PlanGeometry.GetOutline(storey))
        {
            scene.Overlay.AddLine(ToWorld(scene, corner, scene.Floor), ToWorld(scene, corner, slabTop), FaintColor, OverlayLines.Thin);
        }

        foreach (BuildingStairs stairs in file.Stairs)
        {
            if (stairs.Level != _level)
            {
                continue;
            }

            StairsLayout layout = BuildingMaker.GetStairsLayout(file, stairs);
            Transform place = BuildingMaker.GetStairsPlace(file, stairs);
            Matrix4x4 toWorld = place.ToMatrix() * scene.Place;
            float climb = slabTop - file.GetFloorHeight(_level) + Lifted;
            foreach ((Vector2 low, Vector2 high) in layout.Openings)
            {
                Span<Vector3> cut = [new(low.X, climb, low.Y), new(high.X, climb, low.Y), new(high.X, climb, high.Y), new(low.X, climb, high.Y)];
                AddFill(scene, cut, toWorld, CutFill, true);
                AddLoop(scene, cut, toWorld, CutColor, OverlayLines.Thin);
            }
        }

        foreach (BuildingElevator elevator in file.Elevators)
        {
            (int from, int to) = BuildingMaker.GetElevatorSpan(file, elevator);
            if (from <= _level && to > _level)
            {
                Vector3[] cut = GetElevatorSquare(file, elevator, _level + 1, Lifted);
                AddFill(scene, cut, scene.Place, CutFill, true);
                AddLoop(scene, cut, scene.Place, CutColor, OverlayLines.Thin);
            }
        }

        foreach (PlanOpening hole in above.Openings)
        {
            if (hole.Kind == OpeningKind.Floor && GetOpeningLoop(file, above, hole, 0, 0) is { } cut)
            {
                AddFill(scene, cut, scene.Place, CutFill, true);
                AddLoop(scene, cut, scene.Place, CutColor, OverlayLines.Thin);
            }
        }
    }

    /// <summary>What a drag in hand shows: the wall being drawn as it will stand with a ring where it joins, a room's four walls.</summary>
    private void DrawDrag(in Scene scene)
    {
        BuildingStorey storey = scene.Storey;
        float height = storey.Height - storey.Floor;
        if (_drag == Drag.Draw)
        {
            Vector2 along = _dragEnd - _dragStart;
            float length = along.Length();
            if (length > 1e-3f)
            {
                Vector2 direction = along / length;
                Quaternion turn = TurnAlong(direction);
                Matrix4x4 frame = Matrix4x4.CreateFromQuaternion(turn) * Matrix4x4.CreateTranslation(_dragStart.X, scene.Floor, _dragStart.Y) * scene.Place;
                float half = storey.Wall / 2;
                DrawBox(scene, frame, new Vector3(0, 0, -half), new Vector3(length, height, half), PickColor, PickFill, OverlayLines.Bold);
            }

            if (_joins is not null)
            {
                Vector3 joined = ToWorld(scene, _dragEnd, scene.Floor + height);
                float ring = _camera.WorldUnitsPerPixel(joined, _height) * 8;
                scene.Overlay.AddCircle(joined, Vector3.UnitY, ring, GreenColor, OverlayLines.Bold);
            }
        }
        else if (_drag == Drag.Rectangle)
        {
            Vector2[] corners = [_dragStart, new(_dragEnd.X, _dragStart.Y), _dragEnd, new(_dragStart.X, _dragEnd.Y)];
            FillPolygon(scene, corners, scene.Floor + Lifted, HoverFill);
            OutlinePolygon(scene, corners, scene.Floor + Lifted, PickColor, OverlayLines.Bold);
            OutlinePolygon(scene, corners, scene.Floor + height, PickColor, OverlayLines.Thin);
            foreach (Vector2 corner in corners)
            {
                scene.Overlay.AddLine(ToWorld(scene, corner, scene.Floor), ToWorld(scene, corner, scene.Floor + height), PickColor, OverlayLines.Thin);
            }
        }
    }

    /// <summary>
    /// The storey's shots as camera marks with the way they look; the picked one with its view down to the floor it looks at and what
    /// sets it off: a room's floor filled, the way through a door, stairs and shafts as boxes, each tied to the shot by a line.
    /// </summary>
    private void DrawShots(in Scene scene)
    {
        BuildingFile file = scene.File;
        foreach (BuildingShot shot in file.Shots)
        {
            bool picked = Picked is { Kind: PlanPickKind.Shot } pick && pick.Id == shot.Id;
            if (shot.Level != _level && !picked)
            {
                continue;
            }

            Vector3 position = ToWorld(scene, shot.Position);
            Vector4 color = picked ? PickColor : LineColor;
            scene.Overlay.AddIcon(position, EditorIcon.Camera, color);
            Vector3 forward = Vector3.Transform(-Vector3.UnitZ, shot.Rotation);
            if (!picked)
            {
                scene.Overlay.AddLine(position, ToWorld(scene, shot.Position + (forward * 1.5f)), color, OverlayLines.Thin);
                continue;
            }

            DrawShotView(scene, shot, forward);
            foreach (ShotTrigger trigger in shot.Triggers)
            {
                DrawTrigger(scene, shot, trigger);
            }
        }
    }

    /// <summary>The picked shot's view: lines from it to the corners of what it sees on its storey's floor.</summary>
    private void DrawShotView(in Scene scene, BuildingShot shot, Vector3 forward)
    {
        float floor = scene.File.GetFloorHeight(shot.Level);
        if (forward.Y > -1e-3f)
        {
            scene.Overlay.AddLine(ToWorld(scene, shot.Position), ToWorld(scene, shot.Position + (forward * 3)), PickColor, OverlayLines.Thin);
            return;
        }

        Vector3 right = Vector3.Transform(Vector3.UnitX, shot.Rotation);
        Vector3 up = Vector3.Transform(Vector3.UnitY, shot.Rotation);
        float halfHeight = Math.Max(shot.Size, MinShotSize) / 2;
        float halfWidth = halfHeight * 16 / 9;
        Span<Vector3> seen = stackalloc Vector3[4];
        ReadOnlySpan<Vector2> corners = [new(-1, -1), new(1, -1), new(1, 1), new(-1, 1)];
        for (int index = 0; index < 4; index++)
        {
            Vector3 origin = shot.Position + (right * (corners[index].X * halfWidth)) + (up * (corners[index].Y * halfHeight));
            float distance = (floor - origin.Y) / forward.Y;
            seen[index] = origin + (forward * distance) + (Vector3.UnitY * Lifted);
        }

        AddLoop(scene, seen, scene.Place, PickColor, OverlayLines.Thin);
        foreach (Vector3 corner in seen)
        {
            AddLine(scene, shot.Position, corner, scene.Place, PickColor with { W = 0.45f }, OverlayLines.Thin);
        }

        float toFloor = (floor - shot.Position.Y) / forward.Y;
        AddLine(scene, shot.Position, shot.Position + (forward * toFloor), scene.Place, PickColor with { W = 0.6f }, OverlayLines.Thin, true);
    }

    private void DrawTrigger(in Scene scene, BuildingShot shot, ShotTrigger trigger)
    {
        BuildingFile file = scene.File;
        if (trigger.Target == TriggerTarget.Room && file.FindStorey(trigger.Level) is { } storey)
        {
            float floor = file.GetFloorHeight(trigger.Level) + Lifted;
            foreach (RoomShape room in PlanGeometry.FindRooms(storey))
            {
                if (room.Record >= 0 && string.Equals(storey.Rooms[room.Record].Name, trigger.Room, StringComparison.OrdinalIgnoreCase))
                {
                    FillPolygon(scene, room.Polygon, floor, TriggerFill);
                    OutlinePolygon(scene, room.Polygon, floor, TriggerColor, OverlayLines.Thin);
                }
            }

            return;
        }

        if (BuildingRegions.GetTrigger(file, trigger) is not { } region)
        {
            return;
        }

        Vector4 color = trigger.Target == TriggerTarget.Opening ? GreenColor : TriggerColor;
        Vector3 half = region.Size / 2;
        Matrix4x4 toWorld = region.Place.ToMatrix() * scene.Place;
        DrawBox(scene, toWorld, region.Middle - half, region.Middle + half, color, color with { W = 0.16f }, OverlayLines.Thin);
        Vector3 middle = Vector3.Transform(region.Middle, toWorld);
        scene.Overlay.AddDashedLine(ToWorld(scene, shot.Position), middle, color with { W = 0.6f }, OverlayLines.Thin);
    }
}
