using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Interaction.Interactables;
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
    private readonly CreationFiles<VehicleFile> _files;
    private readonly IContentManager? _content;
    private readonly VehicleLooks _looks;
    private readonly List<(Entity Root, string File)> _changed = [];
    private readonly HashSet<Entity> _driven = [];

    public VehicleSpawnSystem(World world, CreationParts parts, CreationFiles<VehicleFile> files, VehicleLooks looks, IContentManager? content)
        : base(world)
    {
        _parts = parts;
        _files = files;
        _content = content;
        _looks = looks;
        _looks.Changed += HandleLooksChanged;
        _parts.AddKind<Vehicle>(vehicle => Describe(vehicle.File, true));
        if (_content is not null)
        {
            _content.Changed += HandleChanged;
        }
    }

    public override void Dispose()
    {
        _looks.Changed -= HandleLooksChanged;
        if (_content is not null)
        {
            _content.Changed -= HandleChanged;
        }

        base.Dispose();
    }

    /// <summary>
    /// Whether the vehicle's parts are solid, as they are while it stands; a driven vehicle's are not, as its body on wheels is its
    /// collision.
    /// </summary>
    public void SetSolid(Entity root, bool solid)
    {
        bool changed = solid ? _driven.Remove(root) : _driven.Add(root);
        if (changed && World.IsAlive(root) && World.TryGet(root, out Vehicle vehicle))
        {
            IReadOnlyList<PartData> parts = Describe(vehicle.File, solid);
            _parts.Patch(root, parts);
        }
    }

    /// <summary>The parts of the file as it last could be read; none without content or a file that never could be.</summary>
    private IReadOnlyList<PartData> Describe(string path, bool solid)
    {
        if (_content is null || _files.Read(_content, path) is not { } file)
        {
            return [];
        }

        List<PartData> parts = [];
        foreach ((string id, SolidPart part) in file.GetParts())
        {
            string mesh = VehicleMeshSource.GetName(path, id, _looks.ShowGreyboxes);
            MeshRenderer renderer = new() { Mesh = mesh };
            PartData data = new PartData(id, part.Place).With(renderer);
            if (part.Joint is { } joint)
            {
                data.With(joint);
            }

            if (solid && part.Joint is not null)
            {
                RigidBody door = GetMovingBody(part);
                data.With(door);
            }
            else if (solid)
            {
                StaticBody standing = GetStandingBody(part);
                data.With(standing);
            }

            if (part.Toggle is { } action)
            {
                Toggle toggle = new() { Action = action };
                data.With(toggle);
                UsePoint middle = new() { Offset = part.GetMiddle() };
                data.With(middle);
            }

            parts.Add(data);
        }

        return parts;
    }

    /// <summary>A standing part's collision is its own triangles, so walls, floors and openings are exact.</summary>
    private static StaticBody GetStandingBody(SolidPart part)
    {
        SolidTriangles triangles = part.Mesh.Triangulate();
        Vector3[] positions = [.. triangles.Vertices.Select(vertex => vertex.Position)];
        int[] indices = [.. triangles.Indices.Select(index => (int)index)];
        TriangleCollider collider = new() { Positions = positions, Indices = indices };
        return new StaticBody { Collider = collider };
    }

    /// <summary>A part that moves is a kinematic hull of its points that paths go around, which follows it as its joint swings it.</summary>
    private static RigidBody GetMovingBody(SolidPart part)
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

        Patch(vehicle =>
        {
            string vehiclePath = _content!.GetFullPath(vehicle.File);
            return ContentManager.PathComparer.Equals(vehiclePath, fullPath);
        });
    }

    /// <summary>Every vehicle's parts change between their looks and their greyboxes.</summary>
    private void HandleLooksChanged() => Patch(_ => true);

    private void Patch(Func<Vehicle, bool> matches)
    {
        _changed.Clear();
        World.Query(in Vehicles, (Entity entity, ref Vehicle vehicle) =>
        {
            if (matches(vehicle))
            {
                _changed.Add((entity, vehicle.File));
            }
        });
        foreach ((Entity root, string file) in _changed)
        {
            bool solid = !_driven.Contains(root);
            IReadOnlyList<PartData> parts = Describe(file, solid);
            _parts.Patch(root, parts);
        }
    }
}
