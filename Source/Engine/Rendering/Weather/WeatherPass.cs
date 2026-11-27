using System.Numerics;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.PostProcessing;

namespace FlickerVoid.Engine.Rendering.Weather;

/// <summary>
/// Adds the mood's weather to the lit scene after glass, hidden behind the scene's depth (Weather.hlsl): rain streaks round the
/// focus (else the camera), as many as <see cref="LightingMood.Rain"/> says, falling along the fog's wind and left out indoors,
/// and dust motes as many as <see cref="LightingMood.Dust"/> says, lit only where light reaches them. Its pipeline exists only
/// while there is weather.
/// </summary>
internal sealed class WeatherPass : IRenderPass
{
    /// <summary>The most rain streaks, drawn at full rain.</summary>
    public const int MaxRain = 32768;

    /// <summary>The most dust motes, drawn at full dust.</summary>
    public const int MaxDust = 8192;

    private const uint Rain = 0;
    private const uint Dust = 1;
    private const float FallSpeed = 9;

    private const float RainBrightness = 0.3f;
    private const float RainLength = 0.8f;
    private const float RainWidth = 2;
    private const float DustBrightness = 1.6f;
    private const float DustWidth = 5;

    private static readonly Vector3 RainBox = new(32, 16, 32);
    private static readonly Vector3 DustBox = new(18, 6, 18);

    private readonly IGraphicsDevice _device;
    private readonly ShaderLibrary _shaders;
    private readonly ColorTarget[] _target = new ColorTarget[1];
    private IPipeline? _pipeline;

    public WeatherPass(IGraphicsDevice device, ShaderLibrary shaders)
    {
        _device = device;
        _shaders = shaders;
    }

    public void Prepare(RenderContext context)
    {
        if (!HasWeather(context.Mood))
        {
            _pipeline?.Dispose();
            _pipeline = null;
            return;
        }

        if (_pipeline is null)
        {
            GraphicsPipelineDescription description = new()
            {
                VertexShader = _shaders.Load("Weather", "VertexMain"),
                PixelShader = _shaders.Load("Weather", "PixelMain"),
                Topology = PrimitiveTopology.TriangleStrip,
                CullMode = CullMode.None,
                ColorFormats = [ScreenTargets.HdrFormat],
                DepthFormat = SceneRenderer.DepthFormat,
                Depth = new DepthState(Test: true, Write: false),
                Blend = BlendMode.Additive,
                Name = "Weather",
            };
            _pipeline = _device.CreateGraphicsPipeline(description);
        }
    }

    public void Execute(RenderContext context)
    {
        if (_pipeline is null)
        {
            return;
        }

        LightingMood mood = context.Mood;
        int rain = (int)MathF.Round(Math.Clamp(mood.Rain, 0, 1) * MaxRain);
        int dust = (int)MathF.Round(Math.Clamp(mood.Dust, 0, 1) * MaxDust);
        Matrix4x4.Invert(context.View.View, out Matrix4x4 cameraWorld);
        Vector3 center = context.Focus ?? cameraWorld.Translation;
        float time = context.Time;
        Vector3 wind = mood.FogWind * (2 + mood.FogWindSpeed);
        Vector3 fall = wind - (Vector3.UnitY * FallSpeed);
        Vector3 drift = mood.FogWind * (0.1f + (mood.FogWindSpeed * 0.1f));

        ScreenTargets targets = context.Targets;
        ICommandList commands = context.Commands;
        commands.Barrier(targets.Depth, ResourceState.DepthWrite);
        commands.Barrier(targets.Hdr, ResourceState.RenderTarget);
        _target[0] = new ColorTarget(targets.Hdr);
        DepthTarget depth = new(targets.Depth);
        commands.BeginRendering(_target, depth);
        commands.SetViewport(new Viewport(0, 0, context.RenderWidth, context.RenderHeight));
        commands.SetScissor(0, 0, context.RenderWidth, context.RenderHeight);
        commands.SetPipeline(_pipeline);
        if (rain > 0)
        {
            WeatherConstants constants = new(context.FrameBuffer, Rain, (uint)rain, time, center, RainBrightness, RainBox, RainLength, fall, RainWidth);
            commands.PushConstants(constants);
            commands.Draw(4, rain);
        }

        if (dust > 0)
        {
            WeatherConstants constants = new(context.FrameBuffer, Dust, (uint)dust, time, center, DustBrightness, DustBox, 0, drift, DustWidth);
            commands.PushConstants(constants);
            commands.Draw(4, dust);
        }

        commands.EndRendering();
        commands.Barrier(targets.Hdr, ResourceState.ShaderRead);
    }

    public void Dispose()
    {
        _pipeline?.Dispose();
    }

    private static bool HasWeather(LightingMood mood)
    {
        return mood.Rain > 0 || mood.Dust > 0;
    }
}
