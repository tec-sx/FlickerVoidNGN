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

namespace FlickerVoid.Engine.Props;

/// <summary>
/// Spawns the parts of each placed prop as its entity attaches: each part solid as its own triangles and its doors swinging on
/// their joints; and brings them in line with the file when the content reports it changed. A frame system for its lifetime.
/// </summary>
internal sealed class PropSpawnSystem : BaseSystem<World, GameTime>
{
    private static readonly QueryDescription Props = new QueryDescription().WithAll<Prop>();

    private readonly CreationParts _parts;
    private readonly CreationFiles<PropFile> _files;
    private readonly IContentManager? _content;
    private readonly List<(Entity Root, string File)> _changed = [];

    public PropSpawnSystem(World world, CreationParts parts, CreationFiles<PropFile> files, IContentManager? content)
        : base(world)
    {
        _parts = parts;
        _files = files;
        _content = content;
        _parts.AddKind<Prop>(prop => Describe(prop.File));
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

    private IReadOnlyList<PartData> Describe(string path)
    {
        if (_content is null || _files.Read(_content, path) is not { } file)
        {
            return [];
        }

        List<PartData> parts = [];
        foreach (PropPart part in file.Parts)
        {
            parts.Add(Stand(path, part.Name, part.Part));
        }

        for (int index = 0; index < file.Doors.Count; index++)
        {
            parts.Add(DescribeDoor(path, $"door.{index + 1}", file.Doors[index]));
        }

        return parts;
    }

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

    private static PartData Draw(string path, string id, SolidPart part)
    {
        string mesh = PropMeshSource.GetName(path, id);
        MeshRenderer renderer = new() { Mesh = mesh };
        return new PartData(id, part.Place).With(renderer);
    }

    private static PartData DescribeDoor(string path, string id, SolidPart door)
    {
        if (door.Joint is not { } joint)
        {
            return Stand(path, id, door);
        }

        PartData data = Draw(path, id, door);
        HullCollider collider = new() { Points = [.. door.Mesh.Points] };
        RigidBody body = new() { Collider = collider, Kinematic = true, Obstacle = true };
        data.With(body).With(joint);
        if (door.Toggle is { } action)
        {
            Toggle toggle = new() { Action = action };
            UsePoint middle = new() { Offset = door.GetMiddle() };
            data.With(toggle).With(middle);
        }

        return data;
    }

    private void HandleChanged(string fullPath)
    {
        if (!fullPath.EndsWith(PropFile.Extension, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _changed.Clear();
        World.Query(in Props, (Entity entity, ref Prop prop) =>
        {
            string propPath = _content!.GetFullPath(prop.File);
            if (ContentManager.PathComparer.Equals(propPath, fullPath))
            {
                _changed.Add((entity, prop.File));
            }
        });
        foreach ((Entity root, string file) in _changed)
        {
            IReadOnlyList<PartData> parts = Describe(file);
            _parts.Patch(root, parts);
        }
    }
}
