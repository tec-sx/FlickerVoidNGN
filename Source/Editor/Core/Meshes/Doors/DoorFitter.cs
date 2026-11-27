using System.Numerics;

using FlickerVoid.Editor.Core.Meshes.Booleans;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Meshes.Doors;

/// <summary>
/// Places doors on walls and makes their parts: a wall keeps its uncut solid (<see cref="SolidPart.Source"/>) and every placed door
/// (a part with a <see cref="DoorFit"/>) cuts its box from it, so a door can be moved, changed or removed and the wall made again.
/// The door's slab, its window, the frame and the hinge follow the fit. Needs Manifold's library for the cuts.
/// </summary>
public static class DoorFitter
{
    private const float Speed = 120;
    private const float Through = 50;
    private const float GlassThickness = 0.01f;

    /// <summary>Whether placed doors can be made and cut.</summary>
    public static bool IsAvailable => SolidBoolean.IsAvailable;

    /// <summary>Adds a door with the fit to the creation and makes it and its wall; the door's id, or null when it can't be made.</summary>
    public static string? Place(ICreationFile file, DoorFit fit)
    {
        SolidPart? wall = FindPart(file, fit.Wall);
        if (wall is null || wall.Joint is not null || wall.Fit is not null || !IsAvailable)
        {
            return null;
        }

        wall.Source ??= wall.Mesh;
        SolidPart door = new() { Place = wall.Place, Mesh = SolidMesh.Box(Vector3.One), Fit = fit, Toggle = new Tag("Interaction.Action.Toggle") };
        file.Doors.Add(door);
        if (!Rebuild(file))
        {
            file.Doors.Remove(door);
            return null;
        }

        return $"door.{file.Doors.Count}";
    }

    /// <summary>Makes the wall's cuts final, before its mesh is edited: its doors stay but are no longer placed ones, and the wall keeps no uncut solid.</summary>
    public static void Bake(ICreationFile file, string wallId)
    {
        SolidPart? wall = FindPart(file, wallId);
        if (wall?.Source is null)
        {
            return;
        }

        wall.Source = null;
        foreach (SolidPart door in file.Doors)
        {
            if (door.Fit?.Wall == wallId)
            {
                door.Fit = null;
            }
        }
    }

    /// <summary>Takes a placed door out of the creation and closes its wall again; false for an id that is not a placed door.</summary>
    public static bool Remove(ICreationFile file, string doorId)
    {
        SolidPart? door = FindPart(file, doorId);
        if (door?.Fit is null || !file.Doors.Remove(door))
        {
            return false;
        }

        return Rebuild(file);
    }

    /// <summary>
    /// Makes every placed door and every wall that has doors from the fits again, after one changed; false when a cut can't be made
    /// (the parts then keep what they had).
    /// </summary>
    public static bool Rebuild(ICreationFile file)
    {
        List<(SolidPart Wall, SolidMesh Cut)> walls = [];
        foreach ((string id, SolidPart part) in file.GetParts())
        {
            if (part.Source is not { } source)
            {
                continue;
            }

            SolidMesh? cut = MakeWall(file, id, source);
            if (cut is null)
            {
                return false;
            }

            walls.Add((part, cut));
        }

        List<(SolidPart Door, SolidMesh Slab)> slabs = [];
        foreach (SolidPart door in file.Doors)
        {
            if (door.Fit is not { } fit)
            {
                continue;
            }

            SolidMesh? slab = MakeSlab(fit);
            if (slab is null)
            {
                return false;
            }

            slabs.Add((door, slab));
        }

        foreach ((SolidPart wall, SolidMesh cut) in walls)
        {
            wall.Mesh = cut;
        }

        foreach ((SolidPart door, SolidMesh slab) in slabs)
        {
            SolidPart? wall = FindPart(file, door.Fit!.Wall);
            if (wall is not null)
            {
                door.Place = wall.Place;
            }

            door.Mesh = slab;
            door.Joint = MakeJoint(door.Fit, door.Place.ToMatrix());
        }

        FreeWalls(file);
        return true;
    }

    /// <summary>The box the door's cut takes out of its wall, in the wall's space.</summary>
    public static SolidMesh MakeCutter(DoorFit fit)
    {
        float back = fit.DepthIn;
        if (back <= 0)
        {
            back = Through;
        }

        float width = fit.Width + (fit.Margin * 2);
        float height = fit.Height + (fit.Margin * 2);
        float depth = back + fit.DepthOut;
        Vector3 centre = fit.Centre + (fit.Up * (fit.Sill + (fit.Height / 2))) + (fit.Normal * ((fit.DepthOut - back) / 2));
        return PlaceBox(new Vector3(width, height, depth), centre, fit);
    }

    /// <summary>The door's slab without its window, flush in the wall's face.</summary>
    public static SolidMesh MakeDoorBox(DoorFit fit)
    {
        Vector3 centre = fit.Centre + (fit.Up * (fit.Sill + (fit.Height / 2))) - (fit.Normal * (fit.Thickness / 2));
        return PlaceBox(new Vector3(fit.Width, fit.Height, fit.Thickness), centre, fit);
    }

    /// <summary>The window's box through the door, in the wall's space.</summary>
    public static SolidMesh MakeWindowBox(DoorFit fit)
    {
        float from = fit.Sill + fit.WindowFrom + (fit.WindowHeight / 2);
        Vector3 centre = fit.Centre + (fit.Up * from) - (fit.Normal * (fit.Thickness / 2));
        return PlaceBox(new Vector3(fit.WindowWidth, fit.WindowHeight, fit.Thickness + 0.1f), centre, fit);
    }

    private static SolidMesh? MakeWall(ICreationFile file, string id, SolidMesh source)
    {
        SolidMesh current = source;
        foreach (SolidPart door in file.Doors)
        {
            if (door.Fit is not { } fit || fit.Wall != id)
            {
                continue;
            }

            SolidMesh cutter = MakeCutter(fit);
            SolidMesh? cut = SolidBoolean.Combine(current, cutter, BooleanOperation.Subtract);
            if (cut is null)
            {
                return null;
            }

            current = cut;
        }

        foreach (SolidPart door in file.Doors)
        {
            if (door.Fit is not { FrameWidth: > 0 } fit || fit.Wall != id)
            {
                continue;
            }

            SolidMesh? framed = SolidBoolean.Combine(current, MakeFrame(fit), BooleanOperation.Add);
            if (framed is null)
            {
                return null;
            }

            current = framed;
        }

        return current;
    }

    // The frame is a ring around the door's box, as deep as the wall's cut face to the door's back, set in the wall's opening.
    private static SolidMesh MakeFrame(DoorFit fit)
    {
        float outerWidth = fit.Width + (fit.Margin * 2) + (fit.FrameWidth * 2);
        float outerHeight = fit.Height + fit.Margin + fit.FrameWidth;
        float depth = fit.Thickness + 0.04f;
        Vector3 centre = fit.Centre + (fit.Up * (fit.Sill + (outerHeight / 2) - fit.Margin)) - (fit.Normal * ((depth / 2) - 0.02f));
        SolidMesh outer = PlaceBox(new Vector3(outerWidth, outerHeight, depth), centre, fit);
        Vector3 holeCentre = fit.Centre + (fit.Up * (fit.Sill + (fit.Height / 2))) - (fit.Normal * ((depth / 2) - 0.02f));
        SolidMesh hole = PlaceBox(new Vector3(fit.Width + (fit.Margin * 2), fit.Height + (fit.Margin * 2), depth + 0.02f), holeCentre, fit);
        return SolidBoolean.Combine(outer, hole, BooleanOperation.Subtract) ?? outer;
    }

    private static SolidMesh? MakeSlab(DoorFit fit)
    {
        SolidMesh slab = MakeDoorBox(fit);
        if (!fit.HasWindow)
        {
            return slab;
        }

        SolidMesh? cut = SolidBoolean.Combine(slab, MakeWindowBox(fit), BooleanOperation.Subtract);
        if (cut is null || !fit.Glass)
        {
            return cut;
        }

        Vector3 centre = fit.Centre + (fit.Up * (fit.Sill + fit.WindowFrom + (fit.WindowHeight / 2))) - (fit.Normal * (fit.Thickness / 2));
        SolidMesh glass = PlaceBox(new Vector3(fit.WindowWidth, fit.WindowHeight, GlassThickness), centre, fit);
        return SolidBoolean.Combine(cut, glass, BooleanOperation.Add) ?? cut;
    }

    // The hinge turns about the door's vertical edge on the hinge side; positive turns carry the free edge out of the face for an
    // outward door and into the wall for an inward one.
    private static PartJoint MakeJoint(DoorFit fit, in Matrix4x4 toCreation)
    {
        float side = fit.HingeLeft ? -1 : 1;
        Vector3 pivot = fit.Centre + (fit.Up * (fit.Sill + (fit.Height / 2))) + (fit.Right * (side * fit.Width / 2)) - (fit.Normal * (fit.Thickness / 2));
        float turn = fit.HingeLeft ? -1 : 1;
        if (!fit.Outward)
        {
            turn = -turn;
        }

        Vector3 axis = fit.Up * turn;
        Vector3 worldPivot = Vector3.Transform(pivot, toCreation);
        Vector3 worldAxis = Vector3.Normalize(Vector3.TransformNormal(axis, toCreation));
        return new PartJoint { Kind = JointKind.Hinge, Pivot = worldPivot, Axis = worldAxis, Min = 0, Max = fit.Angle, Speed = Speed };
    }

    // A wall left without doors gives its source back.
    private static void FreeWalls(ICreationFile file)
    {
        foreach ((string id, SolidPart part) in file.GetParts())
        {
            if (part.Source is null)
            {
                continue;
            }

            bool hasDoor = file.Doors.Any(door => door.Fit is { } fit && fit.Wall == id);
            if (!hasDoor)
            {
                part.Mesh = part.Source;
                part.Source = null;
            }
        }
    }

    // A box stands on its origin, so the basis is put half its height lower to centre it on the point.
    private static SolidMesh PlaceBox(Vector3 size, Vector3 centre, DoorFit fit)
    {
        Vector3 up = fit.Up;
        Vector3 origin = centre - (up * (size.Y / 2));
        Matrix4x4 basis = new(
            fit.Right.X, fit.Right.Y, fit.Right.Z, 0,
            up.X, up.Y, up.Z, 0,
            fit.Normal.X, fit.Normal.Y, fit.Normal.Z, 0,
            origin.X, origin.Y, origin.Z, 1);
        return SolidMesh.Box(size).Transform(basis);
    }

    private static SolidPart? FindPart(ICreationFile file, string id)
    {
        foreach ((string partId, SolidPart part) in file.GetParts())
        {
            if (partId == id)
            {
                return part;
            }
        }

        return null;
    }
}
