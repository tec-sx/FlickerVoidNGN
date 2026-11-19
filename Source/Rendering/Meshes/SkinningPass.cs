using System.Numerics;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Shaders;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// Turns the scene's meshes into draws, posing animated ones, then runs the skinning compute work that writes their posed
/// vertex buffers.
/// </summary>
internal sealed class SkinningPass : IRenderPass
{
    private readonly MeshLibrary _meshes;
    private readonly MeshSkinner _skinner;

    public SkinningPass(IGraphicsDevice device, IShaderCompiler compiler, IContentManager content)
    {
        _meshes = new MeshLibrary(device, content);
        _skinner = new MeshSkinner(device, compiler);
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

            AddDraws(context.Draws, mesh.World, model, posed);
        }
    }

    public void Execute(RenderContext context)
    {
        _skinner.Dispatch(context.Commands, context.FrameSlot);
    }

    public void Dispose()
    {
        _skinner.Dispose();
        _meshes.Dispose();
    }

    private static void AddDraws(List<MeshDraw> draws, Matrix4x4 meshWorld, GpuModel model, IBuffer[]? posed)
    {
        for (int index = 0; index < model.Parts.Count; index++)
        {
            GpuMeshPart part = model.Parts[index];
            IBuffer vertices = part.Vertices;
            if (posed is not null)
            {
                vertices = posed[index];
            }

            draws.Add(new MeshDraw(meshWorld, part, vertices));
        }
    }
}
