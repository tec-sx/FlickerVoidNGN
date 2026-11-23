using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Greyboxes;

namespace FlickerVoid.Editor.Core.Selection;

/// <summary>
/// Finds the mesh or greybox a ray hits first, by its triangles (skinned ones in their bind pose); entities without a mesh are picked by
/// their icons. Keeps each model's triangles from the first pick until its file changes.
/// </summary>
public sealed class ScenePicker : IDisposable
{
    private static readonly QueryDescription Meshes = new QueryDescription().WithAll<MeshRenderer, Transform>().WithNone<Hidden>();
    private static readonly QueryDescription Greyboxes = new QueryDescription().WithAll<Greybox, Transform>().WithNone<MeshRenderer, Hidden>();

    private readonly IContentManager _content;
    private readonly Dictionary<string, PickModel?> _models = new(ContentManager.PathComparer);

    public ScenePicker(IContentManager content)
    {
        _content = content;
        _content.Changed += HandleChanged;
    }

    public Entity? Pick(World world, Ray ray)
    {
        return Pick(world, ray, out _);
    }

    /// <summary>The mesh the ray hits first, and how far along the ray it is hit (infinity when nothing is).</summary>
    public Entity? Pick(World world, Ray ray, out float distance)
    {
        Entity? nearest = null;
        float nearestDistance = float.PositiveInfinity;
        world.Query(in Meshes, (Entity entity, ref MeshRenderer renderer, ref Transform transform) =>
        {
            if (HitsMesh(ray, renderer.Mesh, transform, out float distance) && distance < nearestDistance)
            {
                nearest = entity;
                nearestDistance = distance;
            }
        });
        world.Query(in Greyboxes, (Entity entity, ref Greybox greybox, ref Transform transform) =>
        {
            string mesh = GetMesh(greybox);
            if (HitsMesh(ray, mesh, transform, out float distance) && distance < nearestDistance)
            {
                nearest = entity;
                nearestDistance = distance;
            }
        });
        distance = nearestDistance;
        return nearest;
    }

    /// <summary>The box round an entity's model in its own space.</summary>
    public bool TryGetBounds(World world, Entity entity, out Vector3 min, out Vector3 max)
    {
        min = default;
        max = default;
        if (!world.IsAlive(entity))
        {
            return false;
        }

        string mesh;
        if (world.TryGet(entity, out MeshRenderer renderer))
        {
            mesh = renderer.Mesh;
        }
        else if (world.TryGet(entity, out Greybox greybox))
        {
            mesh = GetMesh(greybox);
        }
        else
        {
            return false;
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

    /// <summary>The model a greybox shows: the swap in its place, or its box.</summary>
    private static string GetMesh(in Greybox greybox)
    {
        return greybox.GetShownSwap() ?? GreyboxMesh.GetName(greybox.Size);
    }

    private PickModel? Load(string mesh)
    {
        if (GreyboxMesh.TryGetSize(mesh, out Vector3 size))
        {
            return LoadGreybox(mesh, size);
        }

        string builtPath = Path.ChangeExtension(mesh, ".fvmodel");
        string fullPath = _content.GetFullPath(builtPath);
        if (_models.TryGetValue(fullPath, out PickModel? loaded))
        {
            return loaded;
        }

        PickModel? model = null;
        if (File.Exists(fullPath))
        {
            model = Read(fullPath);
        }

        _models.Add(fullPath, model);
        return model;
    }

    private PickModel LoadGreybox(string mesh, Vector3 size)
    {
        if (_models.TryGetValue(mesh, out PickModel? made) && made is not null)
        {
            return made;
        }

        ModelData data = GreyboxMesh.Create(size);
        PickModel model = Collect(data);
        _models[mesh] = model;
        return model;
    }

    private static PickModel Read(string fullPath)
    {
        ModelData data;
        using (FileStream stream = File.OpenRead(fullPath))
        {
            data = ModelFile.Read(stream);
        }

        return Collect(data);
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

    private void HandleChanged(string fullPath)
    {
        _models.Remove(fullPath);
    }
}
