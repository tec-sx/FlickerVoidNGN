using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// Lights the fog once a frame in a small grid along the view (160 by 90 froxels, 64 slices out to the mood's fog range:
/// logarithmic in a perspective view, even in an orthographic one), with the sun through its shadow map, the cluster lights and
/// the probes (Lighting.hlsl), keeps most of the frame before's lighting by reprojecting it, and integrates every column of the
/// grid front to back, so the lighting pass reads the fog in front of any surface with one lookup. It exists only while the mood
/// has fog.
/// </summary>
internal sealed class FogVolumePass : IRenderPass
{
    public const int Width = 160;

    public const int Height = 90;

    public const int Slices = 64;

    // A froxel holds a float4: the light scattered, and the density or the light that gets through.
    private const int CellSize = 16;

    private const int GroupSize = 8;

    private const int SliceGroup = 4;

    private readonly IGraphicsDevice _device;
    private readonly IPipeline _light;
    private readonly IPipeline _integrate;
    private IBuffer? _lit;
    private IBuffer? _history;
    private IBuffer? _integrated;
    private bool _hasHistory;
    private uint _number;

    public FogVolumePass(IGraphicsDevice device, ShaderLibrary shaders)
    {
        _device = device;
        ComputePipelineDescription lightDescription = new() { ComputeShader = shaders.Load("Lighting", "FogLightMain"), Name = "Fog light" };
        _light = device.CreateComputePipeline(lightDescription);
        ComputePipelineDescription integrateDescription = new() { ComputeShader = shaders.Load("Lighting", "FogIntegrateMain"), Name = "Fog integrate" };
        _integrate = device.CreateComputePipeline(integrateDescription);
    }

    public void Prepare(RenderContext context)
    {
        if (context.Mood.FogDensity <= 0)
        {
            Release();
            return;
        }

        if (_integrated is null)
        {
            int size = Width * Height * Slices * CellSize;
            BufferUsage usage = BufferUsage.ShaderRead | BufferUsage.ShaderWrite;
            _lit = _device.CreateBuffer(new BufferDescription(size, usage) { Name = "Fog lit" });
            _history = _device.CreateBuffer(new BufferDescription(size, usage) { Name = "Fog history" });
            _integrated = _device.CreateBuffer(new BufferDescription(size, usage) { Name = "Fog integrated" });
            _hasHistory = false;
        }

        context.FogVolume = _integrated;
    }

    public void Execute(RenderContext context)
    {
        if (context.FogVolume is null || _lit is null || _history is null || _integrated is null)
        {
            return;
        }

        bool keepsHistory = _hasHistory && !context.ResetsHistory;
        uint keep = 0;
        if (keepsHistory)
        {
            keep = 1;
        }

        LightingConstants constants = new(context.FrameBuffer, 0, _lit.BindlessIndex, _history.BindlessIndex, _integrated.BindlessIndex, keep, _number);
        ICommandList commands = context.Commands;
        commands.Barrier(_history, ResourceState.ShaderRead);
        commands.Barrier(_lit, ResourceState.ShaderWrite);
        commands.SetPipeline(_light);
        commands.PushConstants(constants);
        commands.Dispatch((Width + GroupSize - 1) / GroupSize, (Height + GroupSize - 1) / GroupSize, Slices / SliceGroup);
        commands.Barrier(_lit, ResourceState.ShaderRead);
        commands.Barrier(_integrated, ResourceState.ShaderWrite);
        commands.SetPipeline(_integrate);
        commands.PushConstants(constants);
        commands.Dispatch((Width + GroupSize - 1) / GroupSize, (Height + GroupSize - 1) / GroupSize);
        commands.Barrier(_integrated, ResourceState.ShaderRead);
        (_lit, _history) = (_history, _lit);
        _hasHistory = true;
        _number++;
    }

    public void Dispose()
    {
        _light.Dispose();
        _integrate.Dispose();
        Release();
    }

    private void Release()
    {
        _lit?.Dispose();
        _history?.Dispose();
        _integrated?.Dispose();
        _lit = null;
        _history = null;
        _integrated = null;
        _hasHistory = false;
    }
}
