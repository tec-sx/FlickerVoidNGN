using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Foliage;

/// <summary>
/// Spawns the parts of each placed plant as its entity attaches (a trunk is solid as its own triangles; limbs and crowns are only
/// drawn), and the copies of each scatter, and brings them in line with the file when the content reports it changed. A frame
/// system for its lifetime: it stops listening when the game is disposed.
/// </summary>
internal sealed class FoliageSpawnSystem : BaseSystem<World, GameTime>
{
    private static readonly QueryDescription Plants = new QueryDescription().WithAll<Plant>();
    private static readonly QueryDescription Scatters = new QueryDescription().WithAll<FoliageScatter>();

    private readonly CreationParts _parts;
    private readonly CreationFiles<FoliageFile> _files;
    private readonly IContentManager? _content;
    private readonly List<(Entity Root, IReadOnlyList<PartData> Parts)> _changed = [];

    public FoliageSpawnSystem(World world, CreationParts parts, CreationFiles<FoliageFile> files, IContentManager? content)
        : base(world)
    {
        _parts = parts;
        _files = files;
        _content = content;
        _parts.AddKind<Plant>(plant => Describe(plant.File));
        _parts.AddKind<FoliageScatter>(Describe);
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
        foreach ((string id, SolidPart part) in file.GetParts())
        {
            PartData data = Draw(path, id, id, part.Place);
            if (id == FoliageFile.TrunkId)
            {
                SolidTriangles triangles = part.Mesh.Triangulate();
                Vector3[] positions = [.. triangles.Vertices.Select(static vertex => vertex.Position)];
                int[] indices = [.. triangles.Indices.Select(static index => (int)index)];
                TriangleCollider collider = new() { Positions = positions, Indices = indices };
                data.With(new StaticBody { Collider = collider });
            }

            parts.Add(data);
        }

        return parts;
    }

    /// <summary>Every part of every copy, its id led by the copy's number: <c>3.crown.1</c>.</summary>
    private IReadOnlyList<PartData> Describe(FoliageScatter scatter)
    {
        if (_content is null || string.IsNullOrEmpty(scatter.File) || _files.Read(_content, scatter.File) is not { } file)
        {
            return [];
        }

        IReadOnlyList<(string Id, SolidPart Part)> own = file.GetParts();
        IReadOnlyList<Transform> places = FoliagePlacement.Scatter(scatter);
        List<PartData> parts = [];
        for (int copy = 0; copy < places.Count; copy++)
        {
            Transform stand = places[copy];
            foreach ((string id, SolidPart part) in own)
            {
                Transform place = new()
                {
                    Position = stand.Position + Vector3.Transform(part.Place.Position * stand.Scale, stand.Rotation),
                    Rotation = stand.Rotation * part.Place.Rotation,
                    Scale = part.Place.Scale * stand.Scale,
                };
                parts.Add(Draw(scatter.File, $"{copy + 1}.{id}", id, place));
            }
        }

        return parts;
    }

    private static PartData Draw(string path, string id, string meshId, Transform place)
    {
        string mesh = FoliageMeshSource.GetName(path, meshId);
        MeshRenderer renderer = new() { Mesh = mesh };
        return new PartData(id, place).With(renderer);
    }

    private void HandleChanged(string fullPath)
    {
        if (!fullPath.EndsWith(FoliageFile.Extension, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _changed.Clear();
        World.Query(in Plants, (Entity entity, ref Plant plant) =>
        {
            if (IsFile(plant.File, fullPath))
            {
                _changed.Add((entity, Describe(plant.File)));
            }
        });
        World.Query(in Scatters, (Entity entity, ref FoliageScatter scatter) =>
        {
            if (IsFile(scatter.File, fullPath))
            {
                _changed.Add((entity, Describe(scatter)));
            }
        });
        foreach ((Entity root, IReadOnlyList<PartData> parts) in _changed)
        {
            _parts.Patch(root, parts);
        }
    }

    private bool IsFile(string file, string fullPath)
    {
        string path = _content!.GetFullPath(file);
        return ContentManager.PathComparer.Equals(path, fullPath);
    }
}
