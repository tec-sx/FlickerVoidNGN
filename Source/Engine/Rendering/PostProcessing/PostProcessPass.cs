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

    public PostProcessPass(IGraphicsDevice device, ShaderLibrary shaders, PixelFormat outputFormat, IContentManager content)
    {
        _post = new PostProcessor(device, shaders, outputFormat);
        _content = content;
    }

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
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

        CanvasView view = CanvasView.From(context.View, context.Camera, context.Height, file.Curvature);
        PostCanvas canvas = new(file, image, view);
        _post.Run(context.Commands, context.Targets, context.ColorTarget, context.Mood, canvas, context.ClampSampler, context.Sampler);
    }

    public void Dispose()
    {
        _post.Dispose();
    }
}
