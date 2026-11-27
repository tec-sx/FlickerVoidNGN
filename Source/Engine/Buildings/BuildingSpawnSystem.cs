using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Cameras.Layers;
using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Cameras.Triggers;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Interaction.Locks;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Buildings;

/// <summary>
/// Spawns the parts of each placed building as its entity attaches: each storey's inside and rooms as indoor volumes, the parts its
/// plan made with their exact collision, door leaves on their hinges with their locks, the roof, stairs and railings, the elevators'
/// platforms and call buttons, and the camera shots with their trigger areas; and brings them in line with the file when the
/// content reports it changed. A frame system for its lifetime: it stops listening when the game is disposed.
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
            DescribeStorey(path, file, storey, parts);
        }

        if (file.Roof.Part is { } roof && !file.Roof.None)
        {
            parts.Add(Stand(path, BuildingFile.RoofId, roof));
        }

        foreach (BuildingStairs stairs in file.Stairs)
        {
            string id = BuildingFile.GetStairsId(stairs);
            if (stairs.Part is { } steps)
            {
                parts.Add(Stand(path, id, steps));
            }

            if (stairs.RailingPart is { } railing)
            {
                parts.Add(Stand(path, $"{id}.railing", railing));
            }
        }

        foreach (BuildingElevator elevator in file.Elevators)
        {
            DescribeElevator(path, file, elevator, parts);
        }

        foreach (BuildingShot shot in file.Shots)
        {
            DescribeShot(file, shot, parts);
        }

        for (int index = 0; index < file.Doors.Count; index++)
        {
            parts.Add(DescribeDoor(path, $"door.{index + 1}", file.Doors[index], null));
        }

        return parts;
    }

    /// <summary>
    /// The storey's inside as an indoor volume (with an old storey's shell), each room as one of its own when it has more than one, and
    /// the parts its plan made: still ones solid as their triangles, and door leaves on their hinges with their locks.
    /// </summary>
    private static void DescribeStorey(string path, BuildingFile file, BuildingStorey storey, List<PartData> parts)
    {
        string storeyId = BuildingFile.GetStoreyId(storey.Level);
        IndoorVolume volume = new() { Ambient = storey.Ambient };
        if (storey.Level < 0)
        {
            volume.OutsideMuted = CellarOutsideMuted;
        }

        PartData inside;
        if (storey.Shell is { } shell)
        {
            Vector3 room = new(file.Size.X - (2 * file.Wall), file.Size.Y, file.Size.Z - (2 * file.Wall));
            volume.Size = room;
            inside = Stand(path, storeyId, shell);
        }
        else if (BuildingRegions.GetStorey(file, storey) is { } region)
        {
            volume.Size = region.Size;
            inside = new PartData(storeyId, region.Place);
        }
        else
        {
            inside = new PartData(storeyId, new Transform());
        }

        parts.Add(inside.With(volume));
        List<(string Name, float Ambient, BuildingRegion Region)> rooms = BuildingRegions.GetRooms(file, storey);
        if (rooms.Count > 1)
        {
            for (int index = 0; index < rooms.Count; index++)
            {
                (_, float ambient, BuildingRegion region) = rooms[index];
                IndoorVolume roomVolume = new() { Size = region.Size, Ambient = ambient, OutsideMuted = volume.OutsideMuted };
                parts.Add(new PartData($"{storeyId}.room.{index + 1}", region.Place).With(roomVolume));
            }
        }

        foreach ((string local, SolidPart part) in storey.Parts)
        {
            string id = BuildingFile.GetPartId(storey, local);
            PlanOpening? opening = FindDoor(storey, local);
            if (part.Joint is not null)
            {
                parts.Add(DescribeDoor(path, id, part, opening));
            }
            else
            {
                parts.Add(Stand(path, id, part));
            }
        }
    }

    /// <summary>The door opening whose leaf has the local id, <c>door.4</c>.</summary>
    private static PlanOpening? FindDoor(BuildingStorey storey, string local)
    {
        const string Prefix = "door.";
        if (!local.StartsWith(Prefix, StringComparison.Ordinal) || !int.TryParse(local.AsSpan(Prefix.Length), out int id))
        {
            return null;
        }

        return storey.Openings.Find(opening => opening.Id == id);
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

    /// <summary>
    /// A part that moves: drawn as its solid, with a kinematic hull of its points that paths go around when it is an obstacle (a door
    /// leaf); a platform is not, so paths keep the floor under and around it and a rider walks off it at any stop.
    /// </summary>
    private static PartData Move(string path, string id, SolidPart part, bool obstacle)
    {
        PartData data = Draw(path, id, part);
        HullCollider collider = new() { Points = [.. part.Mesh.Points] };
        RigidBody body = new() { Collider = collider, Kinematic = true, Obstacle = obstacle };
        return data.With(body);
    }

    private static PartData Draw(string path, string id, SolidPart part)
    {
        string mesh = BuildingMeshSource.GetName(path, id);
        MeshRenderer renderer = new() { Mesh = mesh };
        return new PartData(id, part.Place).With(renderer);
    }

    /// <summary>A door on its joint, opened by its toggle and reached at its middle; locked as its opening says.</summary>
    private static PartData DescribeDoor(string path, string id, SolidPart door, PlanOpening? opening)
    {
        if (door.Joint is not { } joint)
        {
            return Stand(path, id, door);
        }

        PartData data = Move(path, id, door, obstacle: true).With(joint);
        if (door.Toggle is { } action)
        {
            Toggle toggle = new() { Action = action };
            UsePoint middle = new() { Offset = door.GetMiddle() };
            data.With(toggle).With(middle);
        }

        if (opening is { Locked: true })
        {
            Lockable doorLock = new()
            {
                Locked = true,
                Key = opening.Key,
                ConsumeKey = opening.ConsumeKey,
                Pickable = opening.Pickable,
                Difficulty = opening.Difficulty,
            };
            data.With(doorLock);
        }

        return data;
    }

    /// <summary>
    /// The platform slides up its shaft between the floors it stops at, ridden standing on it; its railing stands still, and each
    /// call button calls it to its storey.
    /// </summary>
    private static void DescribeElevator(string path, BuildingFile file, BuildingElevator elevator, List<PartData> parts)
    {
        string id = BuildingFile.GetElevatorId(elevator);
        (int Level, float Height)[] stops = file.GetElevatorStops(elevator);
        if (elevator.Platform is { } platform && stops.Length > 0)
        {
            float[] heights = [.. stops.Select(static stop => stop.Height)];
            int[] levels = [.. stops.Select(static stop => stop.Level)];
            PartJoint joint = new() { Kind = JointKind.Slider, Axis = Vector3.UnitY, Min = 0, Max = heights[^1], Speed = elevator.Speed };
            Toggle toggle = new() { Action = Ride };
            Elevator stopsAt = new() { Stops = heights, Levels = levels };
            Vector3 top = platform.GetMiddle();
            top.Y *= 2;
            UsePoint stand = new() { Offset = top, StandOn = true };
            parts.Add(Move(path, id, platform, obstacle: false).With(joint).With(toggle).With(stopsAt).With(stand));
        }

        if (elevator.RailingPart is { } railing)
        {
            parts.Add(Stand(path, $"{id}.railing", railing));
        }

        if (!elevator.CallButtons)
        {
            return;
        }

        for (int stop = 0; stop < stops.Length; stop++)
        {
            string callId = BuildingFile.GetCallId(elevator, stops[stop].Level);
            if (FindPart(file, callId) is not { } button)
            {
                continue;
            }

            Toggle press = new() { Action = Ride };
            ElevatorCall call = new() { Platform = id, Stop = stop };
            parts.Add(Stand(path, callId, button).With(press).With(call).With(new UsePoint()));
        }
    }

    private static SolidPart? FindPart(BuildingFile file, string id)
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

    /// <summary>
    /// A shot standing where it was placed, an orthographic camera of its size that follows the target and drifts toward the pointer a
    /// little; and a trigger area for each of its triggers that still names something in the building.
    /// </summary>
    private static void DescribeShot(BuildingFile file, BuildingShot shot, List<PartData> parts)
    {
        string id = BuildingFile.GetShotId(shot);
        Transform place = new() { Position = shot.Position, Rotation = shot.Rotation };
        Camera lens = new() { Projection = CameraProjection.Orthographic, OrthographicHeight = Math.Max(shot.Size, 0.5f), FarPlane = 200 };
        CameraRig rig = new();
        float size = Math.Max(shot.Size, 0.5f);
        if (shot.Follow > 0)
        {
            rig.Layers.Add(new FollowLayer { Strength = Math.Clamp(shot.Follow, 0, 1), Radius = size / 4 });
        }

        if (shot.Look > 0)
        {
            rig.Layers.Add(new LookLayer { Radius = shot.Look });
        }

        PlacedShot placed = new() { Rig = rig };
        parts.Add(new PartData(id, place).With(lens).With(new Shot()).With(placed));
        for (int index = 0; index < shot.Triggers.Count; index++)
        {
            ShotTrigger trigger = shot.Triggers[index];
            if (BuildingRegions.GetTrigger(file, trigger) is not { } region)
            {
                continue;
            }

            TriggerArea area = new() { Size = region.Size, Offset = region.Middle };
            BuildingCue cue = new() { Shot = id, When = trigger.When, Change = trigger.Change, Time = trigger.Time, Return = trigger.Return };
            parts.Add(new PartData($"{id}.trigger.{index + 1}", region.Place).With(area).With(cue));
        }
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
