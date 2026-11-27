using System.Numerics;
using System.Runtime.InteropServices;

using BCnEncoder.Shared;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Resources;
using FlickerVoid.Engine.Graphics.Samplers;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Tools.ShaderCompiler;

namespace FlickerVoid.Engine.Graphics.Tests;

public sealed class GraphicsDeviceTests : IDisposable
{
    private static readonly Vector4 Blue = new(0, 0, 1, 1);
    private static readonly Vector4 Red = new(1, 0, 0, 1);
    private static readonly uint BluePixel = GraphicsDeviceTestsBuilder.Rgba(0, 0, 255, 255);
    private static readonly uint RedPixel = GraphicsDeviceTestsBuilder.Rgba(255, 0, 0, 255);

    private GraphicsDeviceTestsBuilder? _builder;

    public void Dispose() => _builder?.Dispose();

    [Fact]
    public void ReportsItsAdapter()
    {
        using IGraphicsDevice device = CreateDevice();

        device.AdapterName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ReportsItsMemoryAndWhatItHasAllocated()
    {
        using IGraphicsDevice device = CreateDevice();
        GraphicsInfo before = device.GetInfo();
        BufferDescription description = new(1 << 20, BufferUsage.ShaderRead);

        IBuffer buffer = device.CreateBuffer(description);
        GraphicsInfo during = device.GetInfo();
        buffer.Dispose();
        device.WaitIdle();
        GraphicsInfo after = device.GetInfo();

        before.AdapterName.Should().Be(device.AdapterName);
        before.ApiVersion.Should().MatchRegex(@"^1\.\d+\.\d+$");
        before.DeviceMemoryBytes.Should().BeGreaterThan(0);
        during.AllocatedBytes.Should().BeGreaterThanOrEqualTo(before.AllocatedBytes + (1 << 20));
        during.Allocations.Should().Be(before.Allocations + 1);
        after.AllocatedBytes.Should().BeLessThanOrEqualTo(during.AllocatedBytes - (1 << 20));
        after.Allocations.Should().BeLessThan(during.Allocations);
    }

    [Fact]
    public void CopiesInitialBufferDataToTheGpu()
    {
        using IGraphicsDevice device = CreateDevice();
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

    [Fact]
    public void UploadBuffersTakeCpuWrites()
    {
        using IGraphicsDevice device = CreateDevice();
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

    [Fact]
    public void ClearsARenderTarget()
    {
        using IGraphicsDevice device = CreateDevice();
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

    [Fact]
    public void CopiesATextureIntoOneOfTheSameSizeAndAnotherFormatOfTheSameBytes()
    {
        using IGraphicsDevice device = CreateDevice();
        TextureDescription sourceDescription = new(GraphicsDeviceTestsBuilder.TargetSize, GraphicsDeviceTestsBuilder.TargetSize, PixelFormat.Rgba8UNormSrgb, TextureUsage.RenderTarget | TextureUsage.CopySource);
        using ITexture source = device.CreateTexture(sourceDescription);
        TextureDescription destinationDescription = sourceDescription with { Format = PixelFormat.Rgba8UNorm, Usage = TextureUsage.CopyDestination | TextureUsage.CopySource | TextureUsage.ShaderRead };
        using ITexture destination = device.CreateTexture(destinationDescription);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        GraphicsDeviceTestsBuilder.BeginRendering(commands, source, Red);
        commands.EndRendering();
        commands.Barrier(source, ResourceState.CopySource);
        commands.Barrier(destination, ResourceState.CopyDestination);
        commands.CopyTexture(source, destination);
        commands.End();
        device.Submit(commands);

        uint[] pixels = GraphicsDeviceTestsBuilder.ReadPixels(device, destination);
        pixels.Should().AllBeEquivalentTo(RedPixel, "the bytes are copied as they are, without converting between formats");
    }

    [Fact]
    public void CopiesASmallerTextureIntoACornerOfALargerOne()
    {
        using IGraphicsDevice device = CreateDevice();
        const int Small = GraphicsDeviceTestsBuilder.TargetSize / 2;
        TextureDescription sourceDescription = new(Small, Small, PixelFormat.Rgba8UNorm, TextureUsage.RenderTarget | TextureUsage.CopySource);
        using ITexture source = device.CreateTexture(sourceDescription);
        TextureDescription destinationDescription = new(GraphicsDeviceTestsBuilder.TargetSize, GraphicsDeviceTestsBuilder.TargetSize, PixelFormat.Rgba8UNorm, TextureUsage.RenderTarget | TextureUsage.CopyDestination | TextureUsage.CopySource);
        using ITexture destination = device.CreateTexture(destinationDescription);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        GraphicsDeviceTestsBuilder.BeginRendering(commands, source, Red);
        commands.EndRendering();
        GraphicsDeviceTestsBuilder.BeginRendering(commands, destination, Blue);
        commands.EndRendering();
        commands.Barrier(source, ResourceState.CopySource);
        commands.Barrier(destination, ResourceState.CopyDestination);
        commands.CopyTexture(source, destination, Small, Small);
        commands.End();
        device.Submit(commands);

        uint[] pixels = GraphicsDeviceTestsBuilder.ReadPixels(device, destination);
        int size = GraphicsDeviceTestsBuilder.TargetSize;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inCorner = x >= Small && y >= Small;
                uint expected = inCorner ? RedPixel : BluePixel;
                pixels[(y * size) + x].Should().Be(expected, $"pixel {x},{y} is {(inCorner ? "inside" : "outside")} the copied corner");
            }
        }
    }

    [Fact]
    public void DrawsVerticesPulledFromABindlessBuffer()
    {
        using IGraphicsDevice device = CreateDevice();
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

    [Fact]
    public void DrawsIndexedVertices()
    {
        using IGraphicsDevice device = CreateDevice();
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

    [Fact]
    public void DrawsIndexedVerticesAsItsArgumentsBufferSays()
    {
        using IGraphicsDevice device = CreateDevice();
        GraphicsDeviceTestsBuilder builder = _builder!;

        // The same two triangles as DrawsIndexedVertices, their counts read from a buffer: 6 indices, 1 instance.
        float[] positions = [-1, 1, 0, 1, 1, 0, 1, -1, 0, -1, -1, 0];
        ushort[] indices = [0, 1, 2, 0, 2, 3];
        uint[] arguments = [6, 1, 0, 0, 0];
        byte[] vertexData = MemoryMarshal.AsBytes(positions.AsSpan()).ToArray();
        byte[] indexData = MemoryMarshal.AsBytes(indices.AsSpan()).ToArray();
        byte[] argumentData = MemoryMarshal.AsBytes(arguments.AsSpan()).ToArray();
        BufferDescription vertexDescription = new(vertexData.Length, BufferUsage.ShaderRead);
        BufferDescription indexDescription = new(indexData.Length, BufferUsage.Index);
        BufferDescription argumentDescription = new(argumentData.Length, BufferUsage.Indirect);
        using IBuffer vertices = device.CreateBuffer(vertexDescription, vertexData);
        using IBuffer indexBuffer = device.CreateBuffer(indexDescription, indexData);
        using IBuffer argumentBuffer = device.CreateBuffer(argumentDescription, argumentData);
        using ITexture target = GraphicsDeviceTestsBuilder.CreateTarget(device);
        using IPipeline pipeline = builder.CreatePipeline(device, TestShaders.PulledVertex, TestShaders.SolidColorPixel);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        GraphicsDeviceTestsBuilder.BeginRendering(commands, target, Blue);
        commands.SetPipeline(pipeline);
        commands.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
        DrawConstants constants = new(vertices.BindlessIndex, 0, 0, 0, Red);
        commands.PushConstants(constants);
        commands.DrawIndexedIndirect(argumentBuffer);
        commands.EndRendering();
        commands.End();
        device.Submit(commands);

        uint[] pixels = GraphicsDeviceTestsBuilder.ReadPixels(device, target);
        pixels.Should().AllBeEquivalentTo(RedPixel);
    }

    [Fact]
    public void SamplesABindlessTexture()
    {
        using IGraphicsDevice device = CreateDevice();
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

    [Fact]
    public void GivesEachReadableResourceItsOwnBindlessSlot()
    {
        using IGraphicsDevice device = CreateDevice();
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

    [Fact]
    public void RejectsPushConstantsThatAreTooLargeOrNotWholeWords()
    {
        using IGraphicsDevice device = CreateDevice();
        using ICommandList commands = device.CreateCommandList();
        commands.Begin();

        Action tooLarge = () => commands.PushConstants(new TooLargeConstants());
        Action partialWord = () => commands.PushConstants((ushort)1);

        tooLarge.Should().Throw<ArgumentException>();
        partialWord.Should().Throw<ArgumentException>();
        commands.End();
    }

    [Fact]
    public void DispatchesAComputeShaderThatWritesABuffer()
    {
        using IGraphicsDevice device = CreateDevice();
        const int Words = 128;
        BufferDescription description = new(Words * 4, BufferUsage.ShaderWrite | BufferUsage.CopySource);
        BufferDescription readbackDescription = new(Words * 4, BufferUsage.CopyDestination, MemoryLocation.Readback);
        using IBuffer buffer = device.CreateBuffer(description);
        using IBuffer readback = device.CreateBuffer(readbackDescription);
        using IPipeline pipeline = _builder!.CreateComputePipeline(device, TestShaders.WriteBufferCompute);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        commands.Barrier(buffer, ResourceState.ShaderWrite);
        commands.SetPipeline(pipeline);
        DrawConstants constants = new(buffer.BindlessIndex, 0, 0, 0, Vector4.Zero);
        commands.PushConstants(constants);
        commands.Dispatch(Words / 64);
        commands.Barrier(buffer, ResourceState.CopySource);
        commands.CopyBuffer(buffer, 0, readback, 0, Words * 4);
        commands.End();
        device.Submit(commands);
        device.WaitIdle();

        uint[] words = new uint[Words];
        readback.Read(MemoryMarshal.AsBytes(words.AsSpan()));
        uint[] expected = [.. Enumerable.Range(0, Words).Select(static index => (uint)index * 3)];
        words.Should().Equal(expected);
        buffer.BindlessIndex.Should().NotBe(BindlessIndex.None);
    }

    [Fact]
    public void WritesATextureFromAComputeShader()
    {
        using IGraphicsDevice device = CreateDevice();
        int size = GraphicsDeviceTestsBuilder.TargetSize;
        TextureDescription description = new(size, size, PixelFormat.Rgba8UNorm, TextureUsage.ShaderWrite | TextureUsage.CopySource);
        using ITexture texture = device.CreateTexture(description);
        using IPipeline pipeline = _builder!.CreateComputePipeline(device, TestShaders.FillTextureCompute);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        commands.Barrier(texture, ResourceState.ShaderWrite);
        commands.SetPipeline(pipeline);
        DrawConstants constants = new(0, texture.BindlessIndex, 0, 0, new Vector4(1, 0, 0, 1));
        commands.PushConstants(constants);
        commands.Dispatch(1, 1);
        commands.End();
        device.Submit(commands);

        uint[] pixels = GraphicsDeviceTestsBuilder.ReadPixels(device, texture);
        pixels.Should().AllBeEquivalentTo(RedPixel);
    }

    [Fact]
    public void GeneratesTheMipChainOfInitialData()
    {
        using IGraphicsDevice device = CreateDevice();

        // 2x2 texels of black and white average to mid grey in the 1x1 level.
        uint black = GraphicsDeviceTestsBuilder.Rgba(0, 0, 0, 255);
        uint white = GraphicsDeviceTestsBuilder.Rgba(255, 255, 255, 255);
        uint[] texels = [black, white, white, black];
        byte[] data = MemoryMarshal.AsBytes(texels.AsSpan()).ToArray();
        TextureDescription description = new(2, 2, PixelFormat.Rgba8UNorm, TextureUsage.ShaderRead | TextureUsage.CopySource) { MipLevels = 2 };
        using ITexture texture = device.CreateTexture(description, data);

        uint[] top = GraphicsDeviceTestsBuilder.ReadPixels(device, texture);
        uint[] last = GraphicsDeviceTestsBuilder.ReadPixels(device, texture, mipLevel: 1);

        top.Should().Equal(texels);
        last.Should().ContainSingle();
        byte[] grey = BitConverter.GetBytes(last[0]);
        grey[0].Should().BeInRange(126, 129);
        grey[3].Should().Be(255);
    }

    [Fact]
    public void UploadsEveryLevelGivenInsteadOfFilteringThem()
    {
        using IGraphicsDevice device = CreateDevice();

        // The 1x1 level is green, which filtering the black and white 2x2 level could never give.
        uint black = GraphicsDeviceTestsBuilder.Rgba(0, 0, 0, 255);
        uint white = GraphicsDeviceTestsBuilder.Rgba(255, 255, 255, 255);
        uint green = GraphicsDeviceTestsBuilder.Rgba(0, 255, 0, 255);
        uint[] texels = [black, white, white, black, green];
        byte[] data = MemoryMarshal.AsBytes(texels.AsSpan()).ToArray();
        TextureDescription description = new(2, 2, PixelFormat.Rgba8UNorm, TextureUsage.ShaderRead | TextureUsage.CopySource) { MipLevels = 2 };
        using ITexture texture = device.CreateTexture(description, data);

        uint[] top = GraphicsDeviceTestsBuilder.ReadPixels(device, texture);
        uint[] last = GraphicsDeviceTestsBuilder.ReadPixels(device, texture, mipLevel: 1);

        top.Should().Equal(black, white, white, black);
        last.Should().Equal(green);
    }

    [Fact]
    public void SamplesABlockCompressedTexture()
    {
        using IGraphicsDevice device = CreateDevice();
        GraphicsDeviceTestsBuilder builder = _builder!;

        // A 4x4 orange texture is one BC7 block; its 2x2 and 1x1 levels are a block each too.
        BCnEncoder.Encoder.BcEncoder encoder = new(CompressionFormat.Bc7);
        encoder.OutputOptions.GenerateMipMaps = true;
        byte[] orange = [.. Enumerable.Repeat<byte[]>([255, 128, 0, 255], 16).SelectMany(texel => texel)];
        byte[][] levels = encoder.EncodeToRawBytes(orange, 4, 4, BCnEncoder.Encoder.PixelFormat.Rgba32);
        byte[] data = [.. levels.SelectMany(level => level)];
        TextureDescription textureDescription = new(4, 4, PixelFormat.Bc7UNorm, TextureUsage.ShaderRead) { MipLevels = levels.Length };
        SamplerDescription samplerDescription = new(SamplerFilter.Point, SamplerAddressMode.Clamp);
        using ITexture texture = device.CreateTexture(textureDescription, data);
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
        levels.Should().HaveCount(3);
        foreach (uint pixel in pixels)
        {
            byte[] channels = BitConverter.GetBytes(pixel);
            channels[0].Should().BeInRange(253, 255);
            channels[1].Should().BeInRange(126, 130);
            channels[2].Should().BeInRange(0, 2);
        }
    }

    [Theory]
    [InlineData(PixelFormat.Rgba8UNorm, 10, 3, 40, 3)]
    [InlineData(PixelFormat.R8UNorm, 5, 2, 5, 2)]
    [InlineData(PixelFormat.Bc7UNormSrgb, 10, 10, 48, 3)]
    [InlineData(PixelFormat.Bc7UNorm, 1, 1, 16, 1)]
    public void MeasuresRowsInBlocksOfCompressedFormats(PixelFormat format, int width, int height, int rowSize, int rowCount)
    {
        format.GetRowSize(width).Should().Be(rowSize);
        format.GetRowCount(height).Should().Be(rowCount);
    }

    [Fact]
    public void MeasuresTheDataOfEveryLevel()
    {
        TextureDescription description = new(10, 10, PixelFormat.Bc7UNorm, TextureUsage.ShaderRead) { MipLevels = 4 };

        // 10x10, 5x5, 2x2 and 1x1 texels are 3x3, 2x2, 1 and 1 blocks of 16 bytes.
        description.GetLevelSize(1).Should().Be(64);
        description.GetDataSize().Should().Be((9 + 4 + 1 + 1) * 16);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(256, 64, 9)]
    [InlineData(5, 3, 3)]
    public void CountsTheLevelsOfAFullMipChain(int width, int height, int levels)
    {
        TextureDescription.CountMipLevels(width, height).Should().Be(levels);
    }

    private IGraphicsDevice CreateDevice()
    {
        _builder = new GraphicsDeviceTestsBuilder();
        return _builder.Build();
    }
}
