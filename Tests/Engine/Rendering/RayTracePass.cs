using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>
/// Once the passes before it drew the frame, notes whether it traced rays and, when it did, traces the rays against the
/// acceleration structure its frame data names (Shaders/RayTrace.hlsl), copying the hits into a readback buffer.
/// </summary>
internal sealed class RayTracePass : IRenderPass
{
    private const int RaySize = 32;
    private const int HitSize = 32;
    private const int HitWords = HitSize / sizeof(uint);
    private const int GroupSize = 64;

    private readonly IGraphicsDevice _device;
    private readonly (Vector3 Origin, Vector3 Direction)[] _rays;
    private readonly IBuffer _rayBuffer;
    private readonly IBuffer _hits;
    private readonly IBuffer _readback;
    private IPipeline? _pipeline;

    public RayTracePass(IGraphicsDevice device, (Vector3 Origin, Vector3 Direction)[] rays)
    {
        _device = device;
        _rays = rays;
        byte[] rayData = new byte[rays.Length * RaySize];
        for (int index = 0; index < rays.Length; index++)
        {
            Vector4 origin = new(rays[index].Origin, 1);
            Vector4 direction = new(rays[index].Direction, 0);
            MemoryMarshal.Write(rayData.AsSpan(index * RaySize), in origin);
            MemoryMarshal.Write(rayData.AsSpan((index * RaySize) + 16), in direction);
        }

        BufferDescription raysDescription = new(rayData.Length, BufferUsage.ShaderRead, MemoryLocation.Upload) { Name = "Rays" };
        _rayBuffer = device.CreateBuffer(raysDescription);
        _rayBuffer.Write(rayData);
        BufferDescription hitsDescription = new(rays.Length * HitSize, BufferUsage.ShaderWrite | BufferUsage.CopySource) { Name = "Hits" };
        _hits = device.CreateBuffer(hitsDescription);
        BufferDescription readbackDescription = new(rays.Length * HitSize, BufferUsage.CopyDestination, MemoryLocation.Readback);
        _readback = device.CreateBuffer(readbackDescription);
    }

    /// <summary>Whether the frame traced rays, known once it ran.</summary>
    public bool TracedRays { get; private set; }

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        TracedRays = context.RayTracing;
        if (!TracedRays)
        {
            return;
        }

        _pipeline ??= CreatePipeline();
        ICommandList commands = context.Commands;
        commands.Barrier(_hits, ResourceState.ShaderWrite);
        commands.SetPipeline(_pipeline);
        TraceConstants constants = new(context.FrameBuffer, _rayBuffer.BindlessIndex, _hits.BindlessIndex, (uint)_rays.Length);
        commands.PushConstants(constants);
        commands.Dispatch((_rays.Length + GroupSize - 1) / GroupSize);
        commands.Barrier(_hits, ResourceState.CopySource);
        commands.CopyBuffer(_hits, 0, _readback, 0, _rays.Length * HitSize);
    }

    /// <summary>
    /// What each ray hit first, with where the mesh of the draw record it names stands, read once the frame finished; all misses
    /// when the frame traced no rays.
    /// </summary>
    public RayHit[] ReadHits()
    {
        RayHit[] hits = new RayHit[_rays.Length];
        if (!TracedRays)
        {
            Array.Fill(hits, new RayHit(RayHit.MissedDraw, null, 0));
            return hits;
        }

        uint[] words = new uint[_rays.Length * HitWords];
        _readback.Read(MemoryMarshal.AsBytes(words.AsSpan()));
        for (int index = 0; index < hits.Length; index++)
        {
            ReadOnlySpan<uint> hit = words.AsSpan(index * HitWords, HitWords);
            ReadOnlySpan<float> values = MemoryMarshal.Cast<uint, float>(hit);
            Vector3? position = null;
            if (hit[0] != RayHit.MissedDraw)
            {
                position = new Vector3(values[2], values[3], values[4]);
            }

            hits[index] = new RayHit(hit[0], position, values[1]);
        }

        return hits;
    }

    public void Dispose()
    {
        _pipeline?.Dispose();
        _readback.Dispose();
        _hits.Dispose();
        _rayBuffer.Dispose();
    }

    private IPipeline CreatePipeline()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "Content", "Tests");
        ShaderLibrary shaders = new(folder);
        ComputePipelineDescription description = new() { ComputeShader = shaders.Load("RayTrace", "ComputeMain"), Name = "Ray trace" };
        return _device.CreateComputePipeline(description);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct TraceConstants(uint Frame, uint Rays, uint Hits, uint Count);
}
