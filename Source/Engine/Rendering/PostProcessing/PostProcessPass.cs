using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>Blooms and grades the HDR scene into the output over the mood's canvas, as the mood says.</summary>
internal sealed class PostProcessPass : IRenderPass
{
    private readonly PostProcessor _post;
    private readonly IContentManager _content;

    public PostProcessPass(IGraphicsDevice device, ShaderLibrary shaders, PixelFormat outputFormat, IContentManager content, int framesInFlight)
    {
        _post = new PostProcessor(device, shaders, outputFormat, framesInFlight);
        _content = content;
    }

    /// <summary>Finds the mood's canvas, which passes that paint into the lit scene read too (the sky).</summary>
    public void Prepare(RenderContext context)
    {
        CanvasFile file = new();
        ITexture? image = null;
        if (context.Mood.Canvas is not null)
        {
            file = _content.Load(context.Mood.Canvas, RenderingJsonContext.Default.CanvasFile);
        }

        if (file.Image is not null)
        {
            image = context.Textures.Get(file.Image);
        }

        CanvasView view = CanvasView.From(context.View, context.Camera, context.Height, file.Tiling);
        context.Canvas = new PostCanvas(file, image, view);
        context.Dirt = null;
        if (context.Mood.Dirt is not null && context.Mood.DirtAmount > 0)
        {
            context.Dirt = context.Textures.Get(context.Mood.Dirt);
        }

        context.Flare = null;
        if (context.Mood.Flare is not null && context.Mood.FlareIntensity > 0)
        {
            context.Flare = _content.Load(context.Mood.Flare, RenderingJsonContext.Default.FlareFile);
        }
    }

    public void Execute(RenderContext context)
    {
        _post.Run(context.Commands, context.Targets, context.ColorTarget, context.Mood, context.Canvas, context.Dirt, context.Flare, context.ClampSampler, context.Sampler);
    }

    public void Dispose()
    {
        _post.Dispose();
    }
}
