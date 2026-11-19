using System.Numerics;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Editor.Viewports;

/// <summary>Draws the ground grid through the origin over the finished image, hidden by the scene in front of it; add it after the post-processing pass.</summary>
public sealed class GridPass : IRenderPass
{
    private readonly IPipeline _pipeline;

    public GridPass(IGraphicsDevice device, ShaderLibrary shaders, PixelFormat colorFormat)
    {
        GraphicsPipelineDescription description = new()
        {
            VertexShader = shaders.Load("Grid", "VertexMain"),
            PixelShader = shaders.Load("Grid", "PixelMain"),
            ColorFormats = [colorFormat],
            DepthFormat = SceneRenderer.DepthFormat,
            Depth = new DepthState(Test: true, Write: false),
            CullMode = CullMode.None,
            Blend = BlendMode.AlphaBlend,
            Name = "Grid",
        };
        _pipeline = device.CreateGraphicsPipeline(description);
    }

    /// <summary>World units between the thin lines.</summary>
    public float CellSize { get; set; } = 1;

    /// <summary>World units between the strong lines.</summary>
    public float MajorCellSize { get; set; } = 10;

    public float Opacity { get; set; } = 1;

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
        commands.SetPipeline(_pipeline);
        Matrix4x4 viewProjection = context.View.ViewProjection;
        Matrix4x4.Invert(viewProjection, out Matrix4x4 inverse);
        Vector4 depthColumn = new(viewProjection.M13, viewProjection.M23, viewProjection.M33, viewProjection.M43);
        Vector4 wColumn = new(viewProjection.M14, viewProjection.M24, viewProjection.M34, viewProjection.M44);
        GridConstants constants = new(inverse, depthColumn, wColumn, CellSize, MajorCellSize, Opacity);
        commands.PushConstants(constants);
        commands.Draw(3);
        commands.EndRendering();
    }

    public void Dispose()
    {
        _pipeline.Dispose();
    }
}
