using System.Numerics;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Editor.Core.Viewports;

/// <summary>
/// Fills what the scene left empty with a sky-like gradient, lighter above the horizon; over a mood's paper it is a thin wash
/// laid over the paper (multiplied), so the paper shows through. Add it after the post-processing pass, before the grid.
/// </summary>
public sealed class SkyPass : IRenderPass
{
    private readonly IPipeline _pipeline;
    private readonly IPipeline _wash;

    public SkyPass(IGraphicsDevice device, ShaderLibrary shaders, PixelFormat colorFormat)
    {
        GraphicsPipelineDescription description = new()
        {
            VertexShader = shaders.Load("Sky", "VertexMain"),
            PixelShader = shaders.Load("Sky", "PixelMain"),
            ColorFormats = [colorFormat],
            DepthFormat = SceneRenderer.DepthFormat,
            Depth = new DepthState(Test: true, Write: false),
            CullMode = CullMode.None,
            Name = "Sky",
        };
        _pipeline = device.CreateGraphicsPipeline(description);
        GraphicsPipelineDescription wash = description with { Blend = BlendMode.Multiply, Name = "Sky wash" };
        _wash = device.CreateGraphicsPipeline(wash);
    }

    public bool Visible { get; set; } = true;

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        if (!Visible)
        {
            return;
        }

        ICommandList commands = context.Commands;
        commands.Barrier(context.Depth, ResourceState.DepthWrite);
        commands.Barrier(context.ColorTarget, ResourceState.RenderTarget);
        ColorTarget color = new(context.ColorTarget, LoadAction.Load);
        DepthTarget depth = new(context.Depth, LoadAction.Load);
        commands.BeginRendering([color], depth);
        commands.SetViewport(new Viewport(0, 0, context.Width, context.Height));
        commands.SetScissor(0, 0, context.Width, context.Height);
        bool onPaper = context.Mood.Canvas is not null;
        if (onPaper)
        {
            commands.SetPipeline(_wash);
        }
        else
        {
            commands.SetPipeline(_pipeline);
        }

        Matrix4x4 viewProjection = context.View.ViewProjection;
        Matrix4x4.Invert(viewProjection, out Matrix4x4 inverse);
        bool orthographic = context.View.Projection.M34 == 0;
        SkyConstants constants = new(inverse, orthographic ? 1 : 0, onPaper ? 1 : 0);
        commands.PushConstants(constants);
        commands.Draw(3);
        commands.EndRendering();
    }

    public void Dispose()
    {
        _pipeline.Dispose();
        _wash.Dispose();
    }
}
