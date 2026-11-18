namespace FlickerVoid.Engine.Graphics;

/// <summary>A GPU and the queue work is submitted to. Create resources and command lists from it, and dispose them before it.</summary>
public interface IGraphicsDevice : IDisposable
{
    GraphicsApi Api { get; }

    string AdapterName { get; }

    /// <summary>Creates a buffer; initial data is copied in before the call returns, leaving a GPU buffer in its shader-read or index state.</summary>
    IBuffer CreateBuffer(in BufferDescription description, ReadOnlySpan<byte> initialData = default);

    /// <summary>Creates a texture; initial data (tightly packed rows) is copied in before the call returns, leaving it in its shader-read state.</summary>
    ITexture CreateTexture(in TextureDescription description, ReadOnlySpan<byte> initialData = default);

    ISampler CreateSampler(in SamplerDescription description);

    IPipeline CreateGraphicsPipeline(GraphicsPipelineDescription description);

    ICommandList CreateCommandList();

    void Submit(ICommandList commandList);

    /// <summary>Blocks until the GPU has finished every submitted command list.</summary>
    void WaitIdle();
}
