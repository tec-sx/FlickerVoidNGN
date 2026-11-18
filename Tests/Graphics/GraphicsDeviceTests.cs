using System.Numerics;
using System.Runtime.InteropServices;
using FlickerVoid.Engine.Shaders;

namespace FlickerVoid.Engine.Graphics.Tests;

public sealed class GraphicsDeviceTests : IDisposable
{
    private static readonly Vector4 Blue = new(0, 0, 1, 1);
    private static readonly Vector4 Red = new(1, 0, 0, 1);
    private static readonly uint BluePixel = GraphicsDeviceTestsBuilder.Rgba(0, 0, 255, 255);
    private static readonly uint RedPixel = GraphicsDeviceTestsBuilder.Rgba(255, 0, 0, 255);

    private GraphicsDeviceTestsBuilder? _builder;

    public static TheoryData<GraphicsApi> Apis => GraphicsDeviceTestsBuilder.Apis;

    public void Dispose() => _builder?.Dispose();

    [Theory]
    [MemberData(nameof(Apis))]
    public void ReportsItsApiAndAdapter(GraphicsApi api)
    {
        using IGraphicsDevice device = CreateDevice(api);

        device.Api.Should().Be(api);
        device.AdapterName.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(Apis))]
    public void CopiesInitialBufferDataToTheGpu(GraphicsApi api)
    {
        using IGraphicsDevice device = CreateDevice(api);
        byte[] data = [.. Enumerable.Range(0, 256).Select(static value => (byte)value)];
        BufferDescription gpuDescription = new(data.Length, BufferUsage.CopySource);
        BufferDescription readbackDescription = new(data.Length, BufferUsage.CopyDestination, MemoryLocation.Readback);
        using IBuffer gpu = device.CreateBuffer(gpuDescription, data);
        using IBuffer readback = device.CreateBuffer(readbackDescription);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        commands.Barrier(gpu, ResourceState.CopySource);
        commands.CopyBuffer(gpu, 0, readback, 0, data.Length);
        commands.End();
        device.Submit(commands);
        device.WaitIdle();

        byte[] read = new byte[data.Length];
        readback.Read(read);
        read.Should().Equal(data);
    }

    [Theory]
    [MemberData(nameof(Apis))]
    public void UploadBuffersTakeCpuWrites(GraphicsApi api)
    {
        using IGraphicsDevice device = CreateDevice(api);
        byte[] data = [1, 2, 3, 4, 5, 6, 7, 8];
        BufferDescription uploadDescription = new(16, BufferUsage.CopySource, MemoryLocation.Upload);
        BufferDescription readbackDescription = new(8, BufferUsage.CopyDestination, MemoryLocation.Readback);
        using IBuffer upload = device.CreateBuffer(uploadDescription);
        using IBuffer readback = device.CreateBuffer(readbackDescription);
        using ICommandList commands = device.CreateCommandList();

        upload.Write(data, 8);
        commands.Begin();
        commands.CopyBuffer(upload, 8, readback, 0, 8);
        commands.End();
        device.Submit(commands);
        device.WaitIdle();

        byte[] read = new byte[8];
        readback.Read(read);
        read.Should().Equal(data);
    }

    [Theory]
    [MemberData(nameof(Apis))]
    public void ClearsARenderTarget(GraphicsApi api)
    {
        using IGraphicsDevice device = CreateDevice(api);
        using ITexture target = GraphicsDeviceTestsBuilder.CreateTarget(device);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        GraphicsDeviceTestsBuilder.BeginRendering(commands, target, Red);
        commands.EndRendering();
        commands.End();
        device.Submit(commands);

        uint[] pixels = GraphicsDeviceTestsBuilder.ReadPixels(device, target);
        pixels.Should().AllBeEquivalentTo(RedPixel);
    }

    [Theory]
    [MemberData(nameof(Apis))]
    public void DrawsVerticesPulledFromABindlessBuffer(GraphicsApi api)
    {
        using IGraphicsDevice device = CreateDevice(api);
        GraphicsDeviceTestsBuilder builder = _builder!;

        // Two triangles covering the top half of the target: y is up in clip space, so the top rows are drawn.
        float[] positions = [-1, 1, 0, 1, 1, 0, 1, 0, 0, -1, 1, 0, 1, 0, 0, -1, 0, 0];
        byte[] vertexData = MemoryMarshal.AsBytes(positions.AsSpan()).ToArray();
        BufferDescription vertexDescription = new(vertexData.Length, BufferUsage.ShaderRead);
        using IBuffer vertices = device.CreateBuffer(vertexDescription, vertexData);
        using ITexture target = GraphicsDeviceTestsBuilder.CreateTarget(device);
        using IPipeline pipeline = builder.CreatePipeline(device, TestShaders.PulledVertex, TestShaders.SolidColorPixel);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        GraphicsDeviceTestsBuilder.BeginRendering(commands, target, Blue);
        commands.SetPipeline(pipeline);
        DrawConstants constants = new(vertices.BindlessIndex, 0, 0, 0, Red);
        commands.PushConstants(constants);
        commands.Draw(6);
        commands.EndRendering();
        commands.End();
        device.Submit(commands);

        uint[] pixels = GraphicsDeviceTestsBuilder.ReadPixels(device, target);
        int half = GraphicsDeviceTestsBuilder.TargetSize * GraphicsDeviceTestsBuilder.TargetSize / 2;
        uint[] expected = [.. Enumerable.Repeat(RedPixel, half), .. Enumerable.Repeat(BluePixel, half)];
        pixels.Should().Equal(expected);
    }

    [Theory]
    [MemberData(nameof(Apis))]
    public void DrawsIndexedVertices(GraphicsApi api)
    {
        using IGraphicsDevice device = CreateDevice(api);
        GraphicsDeviceTestsBuilder builder = _builder!;

        // Two triangles covering the whole target, drawn through an index buffer.
        float[] positions = [-1, 1, 0, 1, 1, 0, 1, -1, 0, -1, -1, 0];
        ushort[] indices = [0, 1, 2, 0, 2, 3];
        byte[] vertexData = MemoryMarshal.AsBytes(positions.AsSpan()).ToArray();
        byte[] indexData = MemoryMarshal.AsBytes(indices.AsSpan()).ToArray();
        BufferDescription vertexDescription = new(vertexData.Length, BufferUsage.ShaderRead);
        BufferDescription indexDescription = new(indexData.Length, BufferUsage.Index);
        using IBuffer vertices = device.CreateBuffer(vertexDescription, vertexData);
        using IBuffer indexBuffer = device.CreateBuffer(indexDescription, indexData);
        using ITexture target = GraphicsDeviceTestsBuilder.CreateTarget(device);
        using IPipeline pipeline = builder.CreatePipeline(device, TestShaders.PulledVertex, TestShaders.SolidColorPixel);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        GraphicsDeviceTestsBuilder.BeginRendering(commands, target, Blue);
        commands.SetPipeline(pipeline);
        commands.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
        DrawConstants constants = new(vertices.BindlessIndex, 0, 0, 0, Red);
        commands.PushConstants(constants);
        commands.DrawIndexed(indices.Length);
        commands.EndRendering();
        commands.End();
        device.Submit(commands);

        uint[] pixels = GraphicsDeviceTestsBuilder.ReadPixels(device, target);
        pixels.Should().AllBeEquivalentTo(RedPixel);
    }

    [Theory]
    [MemberData(nameof(Apis))]
    public void SamplesABindlessTexture(GraphicsApi api)
    {
        using IGraphicsDevice device = CreateDevice(api);
        GraphicsDeviceTestsBuilder builder = _builder!;

        // 2x2 texels: red, green on top; blue, white below. Point sampling maps each to a 2x2 quarter of the target.
        uint green = GraphicsDeviceTestsBuilder.Rgba(0, 255, 0, 255);
        uint white = GraphicsDeviceTestsBuilder.Rgba(255, 255, 255, 255);
        uint[] texels = [RedPixel, green, BluePixel, white];
        byte[] textureData = MemoryMarshal.AsBytes(texels.AsSpan()).ToArray();
        TextureDescription textureDescription = new(2, 2, PixelFormat.Rgba8UNorm, TextureUsage.ShaderRead);
        SamplerDescription samplerDescription = new(SamplerFilter.Point, SamplerAddressMode.Clamp);
        using ITexture texture = device.CreateTexture(textureDescription, textureData);
        using ISampler sampler = device.CreateSampler(samplerDescription);
        using ITexture target = GraphicsDeviceTestsBuilder.CreateTarget(device);
        using IPipeline pipeline = builder.CreatePipeline(device, TestShaders.FullScreenVertex, TestShaders.TexturedPixel);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        GraphicsDeviceTestsBuilder.BeginRendering(commands, target, Vector4.Zero);
        commands.SetPipeline(pipeline);
        DrawConstants constants = new(0, texture.BindlessIndex, sampler.BindlessIndex, 0, Vector4.Zero);
        commands.PushConstants(constants);
        commands.Draw(3);
        commands.EndRendering();
        commands.End();
        device.Submit(commands);

        uint[] pixels = GraphicsDeviceTestsBuilder.ReadPixels(device, target);
        uint[] expected =
        [
            RedPixel, RedPixel, green, green,
            RedPixel, RedPixel, green, green,
            BluePixel, BluePixel, white, white,
            BluePixel, BluePixel, white, white,
        ];
        pixels.Should().Equal(expected);
    }

    [Theory]
    [MemberData(nameof(Apis))]
    public void GivesEachReadableResourceItsOwnBindlessSlot(GraphicsApi api)
    {
        using IGraphicsDevice device = CreateDevice(api);
        BufferDescription readable = new(16, BufferUsage.ShaderRead);
        BufferDescription copyOnly = new(16, BufferUsage.CopySource);
        TextureDescription texture = new(1, 1, PixelFormat.Rgba8UNorm, TextureUsage.ShaderRead);
        using IBuffer first = device.CreateBuffer(readable);
        using IBuffer second = device.CreateBuffer(readable);
        using IBuffer hidden = device.CreateBuffer(copyOnly);
        using ITexture image = device.CreateTexture(texture);

        uint[] slots = [first.BindlessIndex, second.BindlessIndex, image.BindlessIndex];

        slots.Should().OnlyHaveUniqueItems().And.NotContain(BindlessIndex.None);
        hidden.BindlessIndex.Should().Be(BindlessIndex.None);
    }

    private IGraphicsDevice CreateDevice(GraphicsApi api)
    {
        _builder = new GraphicsDeviceTestsBuilder(api);
        return _builder.Build();
    }
}
