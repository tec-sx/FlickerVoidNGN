using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Buildings;

/// <summary>
/// Spawns the parts of each placed building as its entity attaches: its storeys, each an indoor volume with the exact collision of
/// its shell, its roof and stairs, its elevator's platform and its doors; and brings them in line with the file when the content
/// reports it changed. A frame system for its lifetime: it stops listening when the game is disposed.
/// </summary>
internal sealed class BuildingSpawnSystem : BaseSystem<World, GameTime>
{
    /// <summary>The offer action that uses a building's elevator.</summary>
    public static readonly Tag Ride = new("Interaction.Action.Toggle");

    /// <summary>How far the world outside is greyed and darkened while the camera looks into a cellar.</summary>
    private const float CellarOutsideMuted = 0.6f;

    private static readonly QueryDescription Buildings = new QueryDescription().WithAll<Building>();

    private readonly CreationParts _parts;
    private readonly CreationFiles<BuildingFile> _files;
    private readonly IContentManager? _content;
    private readonly List<(Entity Root, string File)> _changed = [];

    public BuildingSpawnSystem(World world, CreationParts parts, CreationFiles<BuildingFile> files, IContentManager? content)
        : base(world)
    {
        _parts = parts;
        _files = files;
        _content = content;
        _parts.AddKind<Building>(building => Describe(building.File));
        if (_content is not null)
        {
            _content.Changed += HandleChanged;
        }
    }

    public override void Dispose()
    {
        if (_content is not null)
        {
            _content.Changed -= HandleChanged;
        }

        base.Dispose();
    }

    /// <summary>The parts of the file as it last could be read; none without content or a file that never could be.</summary>
    private IReadOnlyList<PartData> Describe(string path)
    {
        if (_content is null || _files.Read(_content, path) is not { } file)
        {
            return [];
        }

        List<PartData> parts = [];
        foreach (BuildingStorey storey in file.Storeys)
        {
            string id = BuildingFile.GetStoreyId(storey.Level);
            PartData data = Stand(path, id, storey.Shell);
            Vector3 room = new(file.Size.X - (2 * file.Wall), file.Size.Y, file.Size.Z - (2 * file.Wall));
            IndoorVolume volume = new() { Size = room, Ambient = storey.Ambient };
            if (storey.Level < 0)
            {
                volume.OutsideMuted = CellarOutsideMuted;
            }

            data.With(volume);
            parts.Add(data);
        }

        parts.Add(Stand(path, BuildingFile.RoofId, file.Roof));
        for (int index = 0; index < file.Stairs.Count; index++)
        {
            parts.Add(Stand(path, $"stairs.{index + 1}", file.Stairs[index]));
        }

        if (file.Elevator is { } elevator)
        {
            parts.Add(DescribeElevator(path, file, elevator));
        }

        for (int index = 0; index < file.Doors.Count; index++)
        {
            parts.Add(DescribeDoor(path, $"door.{index + 1}", file.Doors[index]));
        }

        return parts;
    }

    /// <summary>A part that stands still: drawn as its solid, and solid as its own triangles, so floors, walls and openings are exact.</summary>
    private static PartData Stand(string path, string id, SolidPart part)
    {
        PartData data = Draw(path, id, part);
        SolidTriangles triangles = part.Mesh.Triangulate();
        Vector3[] positions = [.. triangles.Vertices.Select(vertex => vertex.Position)];
        int[] indices = [.. triangles.Indices.Select(index => (int)index)];
        TriangleCollider collider = new() { Positions = positions, Indices = indices };
        StaticBody body = new() { Collider = collider };
        return data.With(body);
    }

    /// <summary>A part that moves: drawn as its solid, with a kinematic hull of its points that paths go around.</summary>
    private static PartData Move(string path, string id, SolidPart part)
    {
        PartData data = Draw(path, id, part);
        HullCollider collider = new() { Points = [.. part.Mesh.Points] };
        RigidBody body = new() { Collider = collider, Kinematic = true, Obstacle = true };
        return data.With(body);
    }

    private static PartData Draw(string path, string id, SolidPart part)
    {
        string mesh = BuildingMeshSource.GetName(path, id);
        MeshRenderer renderer = new() { Mesh = mesh };
        return new PartData(id, part.Place).With(renderer);
    }

    private static PartData DescribeDoor(string path, string id, SolidPart door)
    {
        if (door.Joint is not { } joint)
        {
            return Stand(path, id, door);
        }

        PartData data = Move(path, id, door).With(joint);
        if (door.Toggle is { } action)
        {
            Toggle toggle = new() { Action = action };
            UsePoint middle = new() { Offset = door.GetMiddle() };
            data.With(toggle).With(middle);
        }

        return data;
    }

    /// <summary>The platform slides up its shaft between the storeys' floors, and is used standing on it.</summary>
    private static PartData DescribeElevator(string path, BuildingFile file, BuildingElevator elevator)
    {
        float[] stops = file.GetElevatorStops();
        PartJoint joint = new() { Kind = JointKind.Slider, Axis = Vector3.UnitY, Min = 0, Max = stops[^1], Speed = elevator.Speed };
        Toggle toggle = new() { Action = Ride };
        Elevator stopsAt = new() { Stops = stops };
        Vector3 top = elevator.Platform.GetMiddle();
        top.Y *= 2;
        UsePoint stand = new() { Offset = top, StandOn = true };
        return Move(path, BuildingFile.ElevatorId, elevator.Platform).With(joint).With(toggle).With(stopsAt).With(stand);
    }

    private void HandleChanged(string fullPath)
    {
        if (!fullPath.EndsWith(BuildingFile.Extension, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _changed.Clear();
        World.Query(in Buildings, (Entity entity, ref Building building) =>
        {
            string buildingPath = _content!.GetFullPath(building.File);
            if (ContentManager.PathComparer.Equals(buildingPath, fullPath))
            {
                _changed.Add((entity, building.File));
            }
        });
        foreach ((Entity root, string file) in _changed)
        {
            IReadOnlyList<PartData> parts = Describe(file);
            _parts.Patch(root, parts);
        }
    }
}
