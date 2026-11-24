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
/// Turns the scene's meshes into draws, posing animated ones and culling those wholly outside the view, then runs the skinning
/// compute work that writes their posed vertex buffers.
/// </summary>
internal sealed class SkinningPass : IRenderPass
{
    private readonly MeshLibrary _meshes;
    private readonly MeshSkinner _skinner;
    private readonly MaterialLibrary _materials;

    public SkinningPass(IGraphicsDevice device, ShaderLibrary shaders, IContentManager content)
    {
        _meshes = new MeshLibrary(device, content);
        _skinner = new MeshSkinner(device, shaders);
        ShaderParameters parameters = ShaderParameters.Load(shaders, "Scene");
        _materials = new MaterialLibrary(device, content, parameters.Parameters);
    }

    public void Prepare(RenderContext context)
    {
        _skinner.Begin();
        Frustum view = new(context.View.ViewProjection);
        foreach (RenderMesh mesh in context.Scene.Meshes)
        {
            GpuModel model = _meshes.Get(mesh.Mesh);
            IBuffer[]? posed = null;
            if (mesh.Animator is MeshAnimator animator)
            {
                posed = _skinner.Pose(mesh.Id, model, animator);
            }

            AddDraws(context, mesh, model, posed, view);
        }
    }

    public void Execute(RenderContext context)
    {
        _skinner.Dispatch(context.Commands, context.FrameSlot);
    }

    public void Dispose()
    {
        _materials.Dispose();
        _skinner.Dispose();
        _meshes.Dispose();
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

    private void AddDraws(RenderContext context, RenderMesh mesh, GpuModel model, IBuffer[]? posed, in Frustum view)
    {
        for (int index = 0; index < model.Parts.Count; index++)
        {
            GpuMeshPart part = model.Parts[index];
            IBuffer vertices = part.Vertices;
            if (posed is not null)
            {
                vertices = posed[index];
            }

            string? materialPath = null;
            if (mesh.Materials is { } materials && index < materials.Length)
            {
                materialPath = materials[index];
            }

            IBuffer material = _materials.Get(materialPath, context.Textures);
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
            context.Draws.Add(new MeshDraw(mesh.World, part, vertices, material, outline, mesh.Flags, bounds, inView));
        }
    }
}
