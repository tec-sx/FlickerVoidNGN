using System.Numerics;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Cameras.Transitions;

/// <summary>
/// Lays the director's transition over the output, after the outlines: while a cross-fade runs, renders the previous shot's live
/// view with a scene renderer of its own and dissolves it away, then fades the world to the fade colour.
/// </summary>
public sealed class TransitionPass : IRenderPass
{
    private readonly IGraphicsDevice _device;
    private readonly SceneRenderer _owner;
    private readonly IPipeline _pipeline;
    private readonly bool _linearOutput;
    private SceneRenderer? _dissolveRenderer;
    private ITexture? _dissolveTarget;

    /// <summary>Creates the pass for a renderer, drawing into targets of its format.</summary>
    public TransitionPass(SceneRenderer owner)
    {
        _owner = owner;
        _device = owner.Device;
        PixelFormat format = owner.ColorFormat;
        _linearOutput = format is PixelFormat.Bgra8UNormSrgb or PixelFormat.Rgba8UNormSrgb;
        ShaderLibrary shaders = ShaderLibrary.ForEngine("Cameras");
        GraphicsPipelineDescription description = new()
        {
            VertexShader = shaders.Load("Transition", "VertexMain"),
            PixelShader = shaders.Load("Transition", "PixelMain"),
            ColorFormats = [format],
            CullMode = CullMode.None,
            Blend = BlendMode.Premultiplied,
            Name = "Camera transition",
        };
        _pipeline = _device.CreateGraphicsPipeline(description);
    }

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        RenderTransition transition = context.Scene.Transition;
        if (!transition.IsVisible)
        {
            return;
        }

        ICommandList commands = context.Commands;
        uint dissolveTexture = 0;
        if (transition.Dissolve > 0)
        {
            ITexture target = RenderDissolve(context, transition.DissolveFrom);
            dissolveTexture = target.BindlessIndex;
        }

        Vector3 fadeColor = transition.FadeColor;
        if (_linearOutput)
        {
            fadeColor = SrgbToLinear(fadeColor);
        }

        commands.Barrier(context.ColorTarget, ResourceState.RenderTarget);
        ColorTarget output = new(context.ColorTarget);
        commands.BeginRendering([output]);
        commands.SetViewport(new Viewport(0, 0, context.Width, context.Height));
        commands.SetScissor(0, 0, context.Width, context.Height);
        commands.SetPipeline(_pipeline);
        Vector4 color = new(fadeColor, 1);
        TransitionConstants constants = new(dissolveTexture, transition.Dissolve, transition.Fade, 0, color);
        commands.PushConstants(constants);
        commands.Draw(3);
        commands.EndRendering();
    }

    public void Dispose()
    {
        _dissolveTarget?.Dispose();
        _dissolveRenderer?.Dispose();
        _pipeline.Dispose();
    }

    /// <summary>Renders the scene through the previous shot's camera, as the owner draws it, into a target the pixel shader reads.</summary>
    private ITexture RenderDissolve(RenderContext context, in RenderCamera from)
    {
        ITexture target = EnsureTarget(context.Width, context.Height);
        _dissolveRenderer ??= new SceneRenderer(_device, _owner.Content, _owner.ColorFormat) { ProbesFrom = _owner };
        _dissolveRenderer.Mood = context.Mood;
        _dissolveRenderer.LocalLights = context.LocalLights;
        _dissolveRenderer.Probes = context.Probes;
        _dissolveRenderer.Sky = context.Sky;
        _dissolveRenderer.RayTracing = context.RayTracing;
        _dissolveRenderer.Meshes = context.Meshes;
        _dissolveRenderer.Render(context.Commands, context.Scene, target, from.Camera, from.Transform);
        context.Commands.Barrier(target, ResourceState.ShaderRead);
        return target;
    }

    /// <summary>Remakes the target when the size changes; the device releases the old one once the GPU is done with it.</summary>
    private ITexture EnsureTarget(int width, int height)
    {
        if (_dissolveTarget is not null && _dissolveTarget.Description.Width == width && _dissolveTarget.Description.Height == height)
        {
            return _dissolveTarget;
        }

        _dissolveTarget?.Dispose();
        TextureUsage usage = TextureUsage.RenderTarget | TextureUsage.ShaderRead;
        TextureDescription description = new(width, height, _owner.ColorFormat, usage) { Name = "Transition dissolve" };
        _dissolveTarget = _device.CreateTexture(description);
        return _dissolveTarget;
    }

    private static Vector3 SrgbToLinear(Vector3 color)
    {
        return new Vector3(ToLinear(color.X), ToLinear(color.Y), ToLinear(color.Z));
    }

    private static float ToLinear(float value)
    {
        if (value <= 0.04045f)
        {
            return value / 12.92f;
        }

        float scaled = (value + 0.055f) / 1.055f;
        return MathF.Pow(scaled, 2.4f);
    }
}
