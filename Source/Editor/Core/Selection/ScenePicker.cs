using System.Diagnostics.CodeAnalysis;
using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Greyboxes;

namespace FlickerVoid.Editor.Core.Selection;

/// <summary>
/// Finds the mesh or greybox a ray hits first, by its triangles (skinned ones in their bind pose); entities without a mesh are picked by
/// their icons, and a creation's parts pick the creation. Makes each mesh through <see cref="Meshes"/> at its first pick and keeps its
/// triangles until the file it was made from changes.
/// </summary>
public sealed class ScenePicker : IDisposable
{
    private static readonly QueryDescription MeshEntities = new QueryDescription().WithAll<MeshRenderer, Transform>().WithNone<Hidden>();
    private static readonly QueryDescription GreyboxEntities = new QueryDescription().WithAll<Greybox, Transform>().WithNone<MeshRenderer, Hidden>();
    private static readonly QueryDescription PartEntities = new QueryDescription().WithAll<CreationPart, Transform>();

    private readonly IContentManager _content;
    private readonly Dictionary<string, Made> _models = new(ContentManager.PathComparer);
    private MeshSources _meshes = new();

    public ScenePicker(IContentManager content)
    {
        _content = content;
        _content.Changed += HandleChanged;
    }

    /// <summary>Turns mesh names into models; the editor gives it the game's, so the meshes the project's modules make are picked too.</summary>
    public MeshSources Meshes
    {
        get => _meshes;
        set
        {
            if (!ReferenceEquals(value, _meshes))
            {
                _meshes = value;
                _models.Clear();
            }
        }
    }

    public Entity? Pick(World world, Ray ray)
    {
        return Pick(world, ray, out _);
    }

    /// <summary>
    /// The mesh the ray hits first, or the creation whose part it is, such as the vehicle of a wheel, and how far along the ray it is
    /// hit (infinity when nothing is).
    /// </summary>
    public Entity? Pick(World world, Ray ray, out float distance)
    {
        Entity? nearest = FindNearest(world, ray, creation: null, out distance);
        if (nearest is { } hit && world.TryGet(hit, out CreationPart part))
        {
            return part.Root;
        }

        return nearest;
    }

    /// <summary>The part of the creation the ray hits first, past anything else in the way, or null.</summary>
    public Entity? PickPart(World world, Ray ray, Entity creation)
    {
        return FindNearest(world, ray, creation, out _);
    }

    /// <summary>The box round an entity's model in its own space, or round a creation's parts.</summary>
    public bool TryGetBounds(World world, Entity entity, out Vector3 min, out Vector3 max)
    {
        min = default;
        max = default;
        if (!world.IsAlive(entity))
        {
            return false;
        }

        if (!TryGetMesh(world, entity, out string? mesh))
        {
            return TryGetPartBounds(world, entity, out min, out max);
        }

        PickModel? model = Load(mesh);
        if (model is null)
        {
            return false;
        }

        min = model.Min;
        max = model.Max;
        return true;
    }

    public void Dispose()
    {
        _content.Changed -= HandleChanged;
    }

    private bool HitsMesh(Ray ray, string mesh, in Transform transform, out float distance)
    {
        distance = 0;
        PickModel? model = Load(mesh);
        if (model is null)
        {
            return false;
        }

        Matrix4x4 world = transform.ToMatrix();
        if (!Matrix4x4.Invert(world, out Matrix4x4 toLocal))
        {
            return false;
        }

        Ray local = ray.Transform(toLocal);
        if (!local.IntersectBox(model.Min, model.Max, out _))
        {
            return false;
        }

        float nearest = float.PositiveInfinity;
        for (int index = 0; index + 2 < model.Indices.Length; index += 3)
        {
            Vector3 a = model.Positions[model.Indices[index]];
            Vector3 b = model.Positions[model.Indices[index + 1]];
            Vector3 c = model.Positions[model.Indices[index + 2]];
            if (local.IntersectTriangle(a, b, c, out float along) && along < nearest)
            {
                nearest = along;
            }
        }

        if (float.IsPositiveInfinity(nearest))
        {
            return false;
        }

        // The local ray's direction carries the inverse scale, so the hit is measured again in world space.
        Vector3 localHit = local.GetPoint(nearest);
        Vector3 worldHit = Vector3.Transform(localHit, world);
        distance = Vector3.Distance(ray.Origin, worldHit);
        return true;
    }

    /// <summary>The mesh or greybox the ray hits first, among the parts of <paramref name="creation"/> (or the creation itself, for a greybox being shaped) when one is given.</summary>
    private Entity? FindNearest(World world, Ray ray, Entity? creation, out float distance)
    {
        Entity? nearest = null;
        float nearestDistance = float.PositiveInfinity;
        world.Query(in MeshEntities, (Entity entity, ref MeshRenderer renderer, ref Transform transform) =>
        {
            if (IsCandidate(world, entity, creation) && HitsMesh(ray, renderer.Mesh, transform, out float distance) && distance < nearestDistance)
            {
                nearest = entity;
                nearestDistance = distance;
            }
        });
        world.Query(in GreyboxEntities, (Entity entity, ref Greybox greybox, ref Transform transform) =>
        {
            string mesh = GetMesh(greybox);
            if (IsCandidate(world, entity, creation) && HitsMesh(ray, mesh, transform, out float distance) && distance < nearestDistance)
            {
                nearest = entity;
                nearestDistance = distance;
            }
        });
        distance = nearestDistance;
        return nearest;
    }

    private static bool IsCandidate(World world, Entity entity, Entity? creation)
    {
        if (creation is not { } root)
        {
            return true;
        }

        return entity == root || (world.TryGet(entity, out CreationPart part) && part.Root == root);
    }

    private static bool TryGetMesh(World world, Entity entity, [NotNullWhen(true)] out string? mesh)
    {
        if (world.TryGet(entity, out MeshRenderer renderer))
        {
            mesh = renderer.Mesh;
            return true;
        }

        if (world.TryGet(entity, out Greybox greybox))
        {
            mesh = GetMesh(greybox);
            return true;
        }

        mesh = null;
        return false;
    }

    /// <summary>The box round the models of the parts of a creation in its own space; false when it has none.</summary>
    private bool TryGetPartBounds(World world, Entity root, out Vector3 min, out Vector3 max)
    {
        min = default;
        max = default;
        if (!world.TryGet(root, out Transform rootTransform))
        {
            return false;
        }

        Matrix4x4 rootMatrix = rootTransform.ToMatrix();
        if (!Matrix4x4.Invert(rootMatrix, out Matrix4x4 toRoot))
        {
            return false;
        }

        Vector3 low = new(float.PositiveInfinity);
        Vector3 high = new(float.NegativeInfinity);
        world.Query(in PartEntities, (Entity entity, ref CreationPart part, ref Transform transform) =>
        {
            if (part.Root != root || !TryGetMesh(world, entity, out string? mesh) || Load(mesh) is not { } model)
            {
                return;
            }

            Matrix4x4 partMatrix = transform.ToMatrix();
            Matrix4x4 partToRoot = partMatrix * toRoot;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = GetCorner(model.Min, model.Max, corner);
                Vector3 placed = Vector3.Transform(point, partToRoot);
                low = Vector3.Min(low, placed);
                high = Vector3.Max(high, placed);
            }
        });
        if (low.X > high.X)
        {
            return false;
        }

        min = low;
        max = high;
        return true;
    }

    /// <summary>One of the eight corners of a box, by the bits of <paramref name="corner"/>: X, Y and Z, low or high.</summary>
    private static Vector3 GetCorner(Vector3 min, Vector3 max, int corner)
    {
        float x = (corner & 1) == 0 ? min.X : max.X;
        float y = (corner & 2) == 0 ? min.Y : max.Y;
        float z = (corner & 4) == 0 ? min.Z : max.Z;
        return new Vector3(x, y, z);
    }

    /// <summary>The model a greybox shows: the swap in its place, or its box.</summary>
    private static string GetMesh(in Greybox greybox)
    {
        return GreyboxMesh.GetName(greybox);
    }

    private PickModel? Load(string mesh)
    {
        if (_models.TryGetValue(mesh, out Made? loaded))
        {
            return loaded.Model;
        }

        MadeMesh? made = _meshes.Make(mesh, _content);
        PickModel? model = null;
        string? file = null;
        if (made is not null)
        {
            model = Collect(made.Model);
        }

        if (made?.File is not null)
        {
            file = _content.GetFullPath(made.File);
        }

        _models.Add(mesh, new Made(model, file));
        return model;
    }

    private static PickModel Collect(ModelData data)
    {
        List<Vector3> positions = [];
        List<uint> indices = [];
        Vector3 min = new(float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity);
        foreach (MeshPart part in data.Parts)
        {
            uint first = (uint)positions.Count;
            foreach (MeshVertex vertex in part.Vertices)
            {
                positions.Add(vertex.Position);
                min = Vector3.Min(min, vertex.Position);
                max = Vector3.Max(max, vertex.Position);
            }

            foreach (uint index in part.Indices)
            {
                indices.Add(first + index);
            }
        }

        return new PickModel([.. positions], [.. indices], min, max);
    }

    /// <summary>Forgets the meshes made from the file, and the missing ones, which the file may be.</summary>
    private void HandleChanged(string fullPath)
    {
        List<string> changed = [];
        foreach ((string mesh, Made made) in _models)
        {
            if (made.Model is null || (made.File is not null && ContentManager.PathComparer.Equals(made.File, fullPath)))
            {
                changed.Add(mesh);
            }
        }

        foreach (string mesh in changed)
        {
            _models.Remove(mesh);
        }
    }

    /// <summary>A mesh's triangles, or null when nothing makes it, and the full path of the file it was made from, if any.</summary>
    private sealed record Made(PickModel? Model, string? File);
}
