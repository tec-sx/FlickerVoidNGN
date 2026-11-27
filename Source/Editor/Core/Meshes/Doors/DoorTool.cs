using System.Numerics;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Solids;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Core.Meshes.Doors;

/// <summary>
/// Hangs a door on a vehicle: a face of a part becomes the opening, a pocket the door's thickness deep (or cut through), and a
/// <c>door.N</c> part fills it flush, a slab of the face's outline hinged on one edge of the face, with the toggle that opens it.
/// </summary>
public static class DoorTool
{
    private const float PivotTolerance = 1e-3f;
    private const float MaxAngle = 170;
    private const float Speed = 120;

    /// <summary>
    /// Makes the door in the face of the part, hinged on the edge, and returns its id; null when the part, face or edge isn't the
    /// vehicle's (a door can't hold another), the options are out of range or a through cut can't be made. A door already hinged on
    /// the same edge is replaced instead of added to.
    /// </summary>
    public static string? Hang(ICreationFile vehicle, string partId, int face, SolidEdge hinge, DoorOptions options)
    {
        if (options.Thickness <= 0 || options.Angle < 1 || options.Angle > MaxAngle)
        {
            return null;
        }

        SolidPart? part = FindPart(vehicle, partId);
        if (part is null || part.Joint is not null || face < 0 || face >= part.Mesh.Faces.Count)
        {
            return null;
        }

        SolidMesh solid = part.Mesh;
        if (!new SolidTopology(solid).GetFaceEdges(face).Contains(hinge))
        {
            return null;
        }

        MeshResult? cut = MeshCut.Faces(solid, new HashSet<int> { face }, options.Thickness, options.Through);
        if (cut is null)
        {
            return null;
        }

        Vector3 normal = GetNormal(solid, face);
        SolidPart door = new() { Place = part.Place, Mesh = MakeSlab(solid, face, normal, options.Thickness) };
        door.Joint = MakeJoint(solid, face, hinge, normal, part.Place.ToMatrix(), options);
        door.Toggle = new Tag("Interaction.Action.Toggle");
        part.Mesh = cut.Solid;
        return Place(vehicle, door);
    }

    /// <summary>
    /// Changes how far a door opens, keeping its hinge, and turns it to swing the other way when <paramref name="reverse"/>; false
    /// for an id that is not a door of the vehicle or an angle out of range.
    /// </summary>
    public static bool Refit(ICreationFile vehicle, string doorId, float angle, bool reverse)
    {
        if (angle < 1 || angle > MaxAngle)
        {
            return false;
        }

        SolidPart? door = FindPart(vehicle, doorId);
        if (door?.Joint is not { } joint)
        {
            return false;
        }

        Vector3 axis = joint.Axis;
        if (reverse)
        {
            axis = -axis;
        }

        door.Joint = joint with { Axis = axis, Min = 0, Max = angle };
        return true;
    }

    private static SolidPart? FindPart(ICreationFile vehicle, string id)
    {
        foreach ((string partId, SolidPart part) in vehicle.GetParts())
        {
            if (partId == id)
            {
                return part;
            }
        }

        return null;
    }

    private static string Place(ICreationFile vehicle, SolidPart door)
    {
        for (int index = 0; index < vehicle.Doors.Count; index++)
        {
            PartJoint? existing = vehicle.Doors[index].Joint;
            if (existing is { } joint && Vector3.Distance(joint.Pivot, door.Joint!.Value.Pivot) < PivotTolerance)
            {
                vehicle.Doors[index] = door;
                return $"door.{index + 1}";
            }
        }

        vehicle.Doors.Add(door);
        return $"door.{vehicle.Doors.Count}";
    }

    // The face's outline and the same outline pushed in by the thickness, closed by sides.
    private static SolidMesh MakeSlab(SolidMesh solid, int face, Vector3 normal, float thickness)
    {
        IReadOnlyList<int> corners = solid.Faces[face];
        int count = corners.Count;
        List<Vector3> points = [];
        foreach (int corner in corners)
        {
            points.Add(solid.Points[corner]);
        }

        foreach (int corner in corners)
        {
            points.Add(solid.Points[corner] - (normal * thickness));
        }

        List<IReadOnlyList<int>> faces = [[.. Enumerable.Range(0, count)], [.. Enumerable.Range(count, count).Reverse()]];
        for (int index = 0; index < count; index++)
        {
            int next = (index + 1) % count;
            faces.Add([index, count + index, count + next, next]);
        }

        return new SolidMesh(points, faces);
    }

    // The hinge turns about the edge, in the vehicle's space; its axis points the way a positive angle swings the door outward
    // (or inward for an inward door), so the open position is the angle itself.
    private static PartJoint MakeJoint(SolidMesh solid, int face, SolidEdge hinge, Vector3 normal, in Matrix4x4 toVehicle, DoorOptions options)
    {
        Vector3 start = solid.Points[hinge.A];
        Vector3 end = solid.Points[hinge.B];
        Vector3 centre = Vector3.Zero;
        foreach (int corner in solid.Faces[face])
        {
            centre += solid.Points[corner];
        }

        centre /= solid.Faces[face].Count;
        Vector3 pivot = (start + end) / 2;
        Vector3 axis = Vector3.Normalize(end - start);
        Vector3 swing = Vector3.Cross(axis, centre - pivot);
        float outward = Vector3.Dot(swing, normal);
        if (outward < 0)
        {
            axis = -axis;
        }

        float angle = options.Angle;
        Vector3 worldPivot = Vector3.Transform(pivot, toVehicle);
        Vector3 worldAxis = Vector3.Normalize(Vector3.TransformNormal(axis, toVehicle));
        float min = 0;
        float max = angle;
        if (!options.Outward)
        {
            worldAxis = -worldAxis;
        }

        return new PartJoint { Kind = JointKind.Hinge, Pivot = worldPivot, Axis = worldAxis, Min = min, Max = max, Speed = Speed };
    }

    private static Vector3 GetNormal(SolidMesh solid, int face)
    {
        IReadOnlyList<int> corners = solid.Faces[face];
        Vector3 sum = Vector3.Zero;
        for (int corner = 0; corner < corners.Count; corner++)
        {
            Vector3 current = solid.Points[corners[corner]];
            Vector3 next = solid.Points[corners[(corner + 1) % corners.Count]];
            sum += Vector3.Cross(current, next);
        }

        return Vector3.Normalize(sum);
    }
}
