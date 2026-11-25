using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.AccelerationStructures;
using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Tools.ShaderCompiler;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Graphics.Tests.AccelerationStructures;

/// <summary>
/// Creates a Vulkan device with validation on, skipping the test without Vulkan 1.3 or, unless asked to leave them out, without
/// ray queries; builds acceleration structures of a quad on it and traces rays against them with a compute shader. Disposing it
/// fails the test on any error the device logged.
/// </summary>
internal sealed class AccelerationStructureTestsBuilder : IDisposable
{
    /// <summary>The bytes between a quad vertex's positions, as in the renderer's meshes.</summary>
    public const int VertexStride = 32;

    /// <summary>A quad of two triangles from -1 to 1 on x and y.</summary>
    public const int QuadVertexCount = 4;

    public const int QuadIndexCount = 6;

    private const int RaySize = 32;
    private const int HitSize = 8;
    private const int GroupSize = 64;

    private const string TraceSource = """
        RaytracingAccelerationStructure AccelerationStructures[] : register(t0, space5);
        ByteAddressBuffer Buffers[] : register(t0, space2);
        RWByteAddressBuffer RWBuffers[] : register(u0, space2);

        struct TraceConstants
        {
            uint Scene;
            uint Rays;
            uint Hits;
            uint Count;
        };

        [[vk::push_constant]] ConstantBuffer<TraceConstants> Trace : register(b0, space0);

        // Each ray is an origin and a direction (float4s); each hit the instance's id, or ~0 on a miss, and the distance.
        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            if (id.x >= Trace.Count)
            {
                return;
            }

            RayDesc ray;
            ray.Origin = Buffers[Trace.Rays].Load<float4>(id.x * 32).xyz;
            ray.Direction = Buffers[Trace.Rays].Load<float4>(id.x * 32 + 16).xyz;
            ray.TMin = 0;
            ray.TMax = 1000;
            RayQuery<RAY_FLAG_NONE> query;
            query.TraceRayInline(AccelerationStructures[Trace.Scene], RAY_FLAG_NONE, 0xFF, ray);
            while (query.Proceed())
            {
            }

            uint2 hit = uint2(0xFFFFFFFF, 0);
            if (query.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
            {
                hit = uint2(query.CommittedInstanceID(), asuint(query.CommittedRayT()));
            }

            RWBuffers[Trace.Hits].Store2(id.x * 8, hit);
        }
        """;

    private readonly RecordingLoggerProvider _logs = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<IDisposable> _created = [];
    private GraphicsFeatures _features = GraphicsFeatures.RayQueries;
    private IGraphicsDevice? _device;
    private IPipeline? _trace;

    public AccelerationStructureTestsBuilder()
    {
        _loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(_logs));
    }

    /// <summary>Creates the device without ray queries, as on a GPU that has none.</summary>
    public AccelerationStructureTestsBuilder WithoutRayQueries()
    {
        _features = GraphicsFeatures.None;
        return this;
    }

    public IGraphicsDevice Build()
    {
        if (_device is not null)
        {
            return _device;
        }

        GraphicsDeviceOptions options = new() { Validation = true, Features = _features };
        try
        {
            _device = new VulkanGraphicsDevice(options, _loggerFactory);
        }
        catch (GraphicsUnavailableException exception)
        {
            Assert.Skip(exception.Message);
            throw;
        }

        if (_features != GraphicsFeatures.None && !_device.Features.HasFlag(GraphicsFeatures.RayQueries))
        {
            Assert.Skip($"{_device.AdapterName} has no ray queries.");
        }

        return _device;
    }

    /// <summary>The quad's triangles with every vertex at the given depth, in a buffer the CPU can rewrite.</summary>
    public BottomLevelGeometry CreateQuad(float depth)
    {
        IGraphicsDevice device = Build();
        BufferDescription vertexDescription = new(QuadVertexCount * VertexStride, BufferUsage.AccelerationStructureInput, MemoryLocation.Upload) { Name = "Quad vertices" };
        IBuffer vertices = Keep(device.CreateBuffer(vertexDescription));
        MoveQuad(vertices, depth);
        uint[] indices = [0, 1, 2, 0, 2, 3];
        BufferDescription indexDescription = new(indices.Length * sizeof(uint), BufferUsage.Index | BufferUsage.AccelerationStructureInput) { Name = "Quad indices" };
        IBuffer indexBuffer = Keep(device.CreateBuffer(indexDescription, MemoryMarshal.AsBytes(indices.AsSpan())));
        return new BottomLevelGeometry(vertices, QuadVertexCount, VertexStride, indexBuffer, QuadIndexCount);
    }

    /// <summary>Moves every vertex of a quad from <see cref="CreateQuad"/> to the given depth.</summary>
    public static void MoveQuad(IBuffer vertices, float depth)
    {
        Vector2[] corners = [new(-1, -1), new(1, -1), new(1, 1), new(-1, 1)];
        byte[] data = new byte[QuadVertexCount * VertexStride];
        for (int index = 0; index < corners.Length; index++)
        {
            Vector3 position = new(corners[index], depth);
            MemoryMarshal.Write(data.AsSpan(index * VertexStride), in position);
        }

        vertices.Write(data);
    }

    public IAccelerationStructure CreateBottomLevel(bool refittable = false)
    {
        IGraphicsDevice device = Build();
        BottomLevelDescription description = new(QuadVertexCount, QuadIndexCount, refittable) { Name = "Quad" };
        return Keep(device.CreateBottomLevel(description));
    }

    public IAccelerationStructure CreateTopLevel(int instanceCapacity)
    {
        IGraphicsDevice device = Build();
        TopLevelDescription description = new(instanceCapacity) { Name = "Scene" };
        return Keep(device.CreateTopLevel(description));
    }

    /// <summary>Records the bottom-level builds, then the top-level build of the instances, and waits for the GPU.</summary>
    public void BuildScene(BottomLevelBuild[] bottomLevels, IAccelerationStructure topLevel, params AccelerationStructureInstance[] instances)
    {
        IGraphicsDevice device = Build();
        int instanceBytes = Math.Max(instances.Length, 1) * Marshal.SizeOf<AccelerationStructureInstance>();
        BufferDescription description = new(instanceBytes, BufferUsage.AccelerationStructureInput, MemoryLocation.Upload) { Name = "Instances" };
        IBuffer instanceBuffer = Keep(device.CreateBuffer(description));
        instanceBuffer.Write(MemoryMarshal.AsBytes(instances.AsSpan()));
        using ICommandList commands = device.CreateCommandList();
        commands.Begin();
        commands.BuildBottomLevels(bottomLevels);
        commands.BuildTopLevel(topLevel, instanceBuffer, instances.Length);
        commands.End();
        device.Submit(commands);
        device.WaitIdle();
    }

    /// <summary>Traces each ray against the top-level structure in that bindless slot: the instance hit first and how far along, or a miss.</summary>
    public RayHit[] Trace(uint scene, params (Vector3 Origin, Vector3 Direction)[] rays)
    {
        IGraphicsDevice device = Build();
        _trace ??= Keep(CreateTracePipeline(device));
        byte[] rayData = new byte[rays.Length * RaySize];
        for (int index = 0; index < rays.Length; index++)
        {
            Vector4 origin = new(rays[index].Origin, 1);
            Vector4 direction = new(rays[index].Direction, 0);
            MemoryMarshal.Write(rayData.AsSpan(index * RaySize), in origin);
            MemoryMarshal.Write(rayData.AsSpan((index * RaySize) + 16), in direction);
        }

        BufferDescription raysDescription = new(rayData.Length, BufferUsage.ShaderRead, MemoryLocation.Upload) { Name = "Rays" };
        using IBuffer rayBuffer = device.CreateBuffer(raysDescription);
        rayBuffer.Write(rayData);
        BufferDescription hitsDescription = new(rays.Length * HitSize, BufferUsage.ShaderWrite | BufferUsage.CopySource) { Name = "Hits" };
        using IBuffer hits = device.CreateBuffer(hitsDescription);
        BufferDescription readbackDescription = new(rays.Length * HitSize, BufferUsage.CopyDestination, MemoryLocation.Readback);
        using IBuffer readback = device.CreateBuffer(readbackDescription);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        commands.Barrier(hits, ResourceState.ShaderWrite);
        commands.SetPipeline(_trace);
        TraceConstants constants = new(scene, rayBuffer.BindlessIndex, hits.BindlessIndex, (uint)rays.Length);
        commands.PushConstants(constants);
        commands.Dispatch((rays.Length + GroupSize - 1) / GroupSize);
        commands.Barrier(hits, ResourceState.CopySource);
        commands.CopyBuffer(hits, 0, readback, 0, rays.Length * HitSize);
        commands.End();
        device.Submit(commands);
        device.WaitIdle();

        uint[] words = new uint[rays.Length * 2];
        readback.Read(MemoryMarshal.AsBytes(words.AsSpan()));
        RayHit[] found = new RayHit[rays.Length];
        for (int index = 0; index < found.Length; index++)
        {
            uint instance = words[index * 2];
            float distance = BitConverter.UInt32BitsToSingle(words[(index * 2) + 1]);
            found[index] = new RayHit(instance, distance);
        }

        return found;
    }

    public void Dispose()
    {
        for (int index = _created.Count - 1; index >= 0; index--)
        {
            _created[index].Dispose();
        }

        _device?.Dispose();
        _loggerFactory.Dispose();
        _logs.Errors.Should().BeEmpty("the device logged no errors");
    }

    private static IPipeline CreateTracePipeline(IGraphicsDevice device)
    {
        using DxcShaderCompiler compiler = new();
        ShaderSource source = new(TraceSource, "main", ShaderStage.Compute);
        ShaderCompileResult result = compiler.Compile(source);
        result.Succeeded.Should().BeTrue(result.Messages);
        ComputePipelineDescription description = new() { ComputeShader = new ShaderBytecode(result.Bytecode), Name = "Trace" };
        return device.CreateComputePipeline(description);
    }

    private T Keep<T>(T resource)
        where T : IDisposable
    {
        _created.Add(resource);
        return resource;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct TraceConstants(uint Scene, uint Rays, uint Hits, uint Count);
}
