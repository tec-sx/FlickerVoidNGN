using System.Numerics;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// Turns the scene's meshes into draws, posing animated ones, then runs the skinning compute work that writes their posed
/// vertex buffers.
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
        foreach (RenderMesh mesh in context.Scene.Meshes)
        {
            GpuModel model = _meshes.Get(mesh.Mesh);
            IBuffer[]? posed = null;
            if (mesh.Animator is MeshAnimator animator)
            {
                posed = _skinner.Pose(mesh.Id, model, animator);
            }

            AddDraws(context.Draws, mesh, model, posed);
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

    private void AddDraws(List<MeshDraw> draws, RenderMesh mesh, GpuModel model, IBuffer[]? posed)
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

            IBuffer material = _materials.Get(materialPath);
            draws.Add(new MeshDraw(mesh.World, part, vertices, material, mesh.Outline));
        }
    }
}
