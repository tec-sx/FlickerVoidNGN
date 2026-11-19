using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Shaders;

namespace FlickerVoid.Engine.Rendering.Shadows;

/// <summary>Draws the depth of the sun and of every shadow-casting light into its tile of the shadow atlas.</summary>
internal sealed class ShadowPass : IRenderPass
{
    private readonly ShadowRenderer _shadows;

    public ShadowPass(IGraphicsDevice device, IShaderCompiler compiler)
    {
        _shadows = new ShadowRenderer(device, compiler);
    }

    public void Prepare(RenderContext context)
    {
        context.ShadowAtlas = _shadows.Atlas.BindlessIndex;
    }

    public void Execute(RenderContext context)
    {
        _shadows.Draw(context.Commands, context.ShadowViews, context.Draws);
    }

    public void Dispose()
    {
        _shadows.Dispose();
    }
}
