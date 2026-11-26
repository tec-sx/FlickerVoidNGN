using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>
/// Spawns the parts of each placed vehicle as its entity attaches, one per part of its file with the part's mesh, and brings them
/// in line with the file as soon as the content reports it changed (in the editor too, which doesn't tick its game). A frame
/// system for its lifetime: it stops listening when the game is disposed.
/// </summary>
internal sealed class VehicleSpawnSystem : BaseSystem<World, GameTime>
{
    private static readonly QueryDescription Vehicles = new QueryDescription().WithAll<Vehicle>();

    private readonly CreationParts _parts;
    private readonly VehicleFiles _files;
    private readonly IContentManager? _content;
    private readonly List<(Entity Root, string File)> _changed = [];

    public VehicleSpawnSystem(World world, CreationParts parts, VehicleFiles files, IContentManager? content)
        : base(world)
    {
        _parts = parts;
        _files = files;
        _content = content;
        _parts.AddKind<Vehicle>(vehicle => Describe(vehicle.File));
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
        foreach ((string id, VehiclePart part) in file.GetParts())
        {
            string mesh = VehicleMeshSource.GetName(path, id);
            MeshRenderer renderer = new() { Mesh = mesh };
            PartData data = new PartData(id, part.Place).With(renderer);
            if (part.Joint is { } joint)
            {
                data.With(joint);
                RigidBody door = GetMovingBody(part);
                data.With(door);
            }
            else
            {
                StaticBody standing = GetStandingBody(part);
                data.With(standing);
            }

            if (part.Toggle is { } action)
            {
                Toggle toggle = new() { Action = action };
                data.With(toggle);
            }

            parts.Add(data);
        }

        return parts;
    }

    /// <summary>A standing part's collision is its own triangles, so walls, floors and openings are exact.</summary>
    private static StaticBody GetStandingBody(VehiclePart part)
    {
        SolidTriangles triangles = part.Mesh.Triangulate();
        Vector3[] positions = [.. triangles.Vertices.Select(vertex => vertex.Position)];
        int[] indices = [.. triangles.Indices.Select(index => (int)index)];
        TriangleCollider collider = new() { Positions = positions, Indices = indices };
        return new StaticBody { Collider = collider };
    }

    /// <summary>A part that moves is a kinematic hull of its points that paths go around, which follows it as its joint swings it.</summary>
    private static RigidBody GetMovingBody(VehiclePart part)
    {
        HullCollider collider = new() { Points = [.. part.Mesh.Points] };
        return new RigidBody { Collider = collider, Kinematic = true, Obstacle = true };
    }

    private void HandleChanged(string fullPath)
    {
        if (!fullPath.EndsWith(VehicleFile.Extension, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _changed.Clear();
        World.Query(in Vehicles, (Entity entity, ref Vehicle vehicle) =>
        {
            string vehiclePath = _content!.GetFullPath(vehicle.File);
            if (ContentManager.PathComparer.Equals(vehiclePath, fullPath))
            {
                _changed.Add((entity, vehicle.File));
            }
        });
        foreach ((Entity root, string file) in _changed)
        {
            IReadOnlyList<PartData> parts = Describe(file);
            _parts.Patch(root, parts);
        }
    }
}
