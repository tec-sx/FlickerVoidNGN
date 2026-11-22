using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>Blooms and grades the HDR scene into the output over the mood's paper, as the mood says.</summary>
internal sealed class PostProcessPass : IRenderPass
{
    private readonly PostProcessor _post;

    public PostProcessPass(IGraphicsDevice device, ShaderLibrary shaders, PixelFormat outputFormat)
    {
        _post = new PostProcessor(device, shaders, outputFormat);
    }

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        ITexture? paper = null;
        if (context.Mood.Paper is not null)
        {
            paper = context.Textures.Get(context.Mood.Paper);
        }

        PaperView paperView = PaperView.From(context.View, context.Camera, context.Height);
        _post.Run(context.Commands, context.Targets, context.ColorTarget, context.Mood, paper, paperView, context.ClampSampler, context.Sampler);
    }

    public void Dispose()
    {
        _post.Dispose();
    }
}
