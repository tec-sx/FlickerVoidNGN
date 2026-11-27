using System.Numerics;

using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Rendering.Instancing;

/// <summary>The sphere round every part of a model, before a copy places it, which the cull sizes as each copy is.</summary>
internal readonly record struct ModelSphere(Vector3 Center, float Radius)
{
    public static ModelSphere Of(GpuModel model)
    {
        Bounds bounds = Bounds.Empty;
        foreach (GpuMeshPart part in model.Parts)
        {
            bounds = bounds.Include(part.Bounds);
        }

        if (bounds.IsEmpty)
        {
            return default;
        }

        return new ModelSphere(bounds.Center, bounds.Extents.Length());
    }
}
