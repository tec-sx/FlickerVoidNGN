using System.Diagnostics;
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
    public const int MaxRain = 8192;

    /// <summary>The most dust motes, drawn at full dust.</summary>
    public const int MaxDust = 2048;

    private const uint Rain = 0;
    private const uint Dust = 1;
    private const float FallSpeed = 9;

    /// <summary>The longest step the particles' time takes between two frames, so a stall doesn't jump them.</summary>
    private const double MaxStep = 0.1;

    private static readonly Vector3 RainBox = new(24, 16, 24);
    private static readonly Vector3 DustBox = new(10, 5, 10);

    private readonly IGraphicsDevice _device;
    private readonly ShaderLibrary _shaders;
    private readonly ColorTarget[] _target = new ColorTarget[1];
    private IPipeline? _pipeline;
    private long _lastTimestamp;
    private double _time;

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

        Advance();
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
        float time = (float)_time;
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
            WeatherConstants constants = new(context.FrameBuffer, Rain, (uint)rain, time, center, 0.12f, RainBox, 0.5f, fall, 1.5f);
            commands.PushConstants(constants);
            commands.Draw(4, rain);
        }

        if (dust > 0)
        {
            WeatherConstants constants = new(context.FrameBuffer, Dust, (uint)dust, time, center, 0.6f, DustBox, 0, drift, 3);
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

    /// <summary>Moves the particles' time on by the time since the last frame, at most <see cref="MaxStep"/>.</summary>
    private void Advance()
    {
        long now = Stopwatch.GetTimestamp();
        if (_lastTimestamp != 0)
        {
            double seconds = Stopwatch.GetElapsedTime(_lastTimestamp, now).TotalSeconds;
            _time += Math.Min(seconds, MaxStep);
        }

        _lastTimestamp = now;
    }
}
