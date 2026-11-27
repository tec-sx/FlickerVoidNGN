using System.Numerics;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Outlines;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// Turns the scene's meshes into draws, posing animated ones and culling those wholly outside the view, with where each mesh
/// stood and how it was posed in the frame before, then runs the skinning compute work that writes their posed vertex buffers.
/// </summary>
internal sealed class SkinningPass : IRenderPass
{
    private readonly MeshLibrary _meshes;
    private readonly MeshSkinner _skinner;
    private readonly MaterialLibrary _materials;
    private readonly PreviousWorlds _previousWorlds = new();

    public SkinningPass(IGraphicsDevice device, ShaderLibrary shaders, MeshLibrary meshes, MaterialLibrary materials)
    {
        _meshes = meshes;
        _skinner = new MeshSkinner(device, shaders);
        _materials = materials;
    }

    public void Prepare(RenderContext context)
    {
        _skinner.Begin();
        _previousWorlds.Begin();
        Frustum view = new(context.View.ViewProjection);
        foreach (RenderMesh mesh in context.Scene.Meshes)
        {
            GpuModel model = _meshes.Get(mesh.Mesh, context.Meshes);
            PosedVertices? posed = null;
            if (mesh.Animator is MeshAnimator animator)
            {
                posed = _skinner.Pose(mesh.Id, model, animator);
            }

            Matrix4x4 previousWorld = _previousWorlds.Remember(mesh.Id, mesh.World);
            AddDraws(context, mesh, previousWorld, model, posed, view);
        }
    }

    public void Execute(RenderContext context)
    {
        _skinner.Dispatch(context.Commands, context.FrameSlot);
    }

    public void Dispose()
    {
        _skinner.Dispose();
    }

    /// <summary>Whether the view draws the part: neither shadow nor outline only, and not wholly outside it; counted when it is shown.</summary>
    private static bool IsInView(RenderStatistics statistics, MeshFlags flags, Bounds? bounds, in Frustum view)
    {
        if ((flags & (MeshFlags.ShadowOnly | MeshFlags.OutlineOnly)) != 0)
        {
            return false;
        }

        if (bounds is { } placed && !view.Intersects(placed))
        {
            statistics.Culled++;
            return false;
        }

        statistics.Drawn++;
        return true;
    }

    private void AddDraws(RenderContext context, RenderMesh mesh, in Matrix4x4 previousWorld, GpuModel model, PosedVertices? posed, in Frustum view)
    {
        for (int index = 0; index < model.Parts.Count; index++)
        {
            GpuMeshPart part = model.Parts[index];
            IBuffer vertices = part.Vertices;
            IBuffer previousVertices = part.Vertices;
            if (posed is { } pose)
            {
                vertices = pose.Current[index];
                previousVertices = pose.Previous[index];
            }

            string? materialPath = null;
            if (mesh.Materials is { } materials && index < materials.Length)
            {
                materialPath = materials[index];
            }

            GpuMaterial material = _materials.Get(materialPath, context.Textures);
            Outline? outline = mesh.Outline;
            if (mesh.Flags.HasFlag(MeshFlags.ShadowOnly))
            {
                outline = null;
            }

            Bounds? bounds = null;
            if (posed is null)
            {
                bounds = part.Bounds.Transform(mesh.World);
            }

            bool inView = IsInView(context.Statistics, mesh.Flags, bounds, view);
            context.Draws.Add(new MeshDraw(mesh.Id, mesh.World, previousWorld, part, vertices, previousVertices, material, outline, mesh.Flags, bounds, inView));
        }
    }
}
