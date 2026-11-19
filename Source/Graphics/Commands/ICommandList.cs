using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Graphics.Commands;

/// <summary>
/// Records GPU work for <see cref="IGraphicsDevice.Submit"/>. Resources move between states only through
/// <see cref="Barrier(ITexture, ResourceState)"/>, called by the code that knows what the resource is used for next.
/// </summary>
public interface ICommandList : IDisposable
{
    /// <summary>Starts recording; waits for the GPU to finish the list's previous submission.</summary>
    void Begin();

    void End();

    void Barrier(ITexture texture, ResourceState state);

    void Barrier(IBuffer buffer, ResourceState state);

    void CopyBuffer(IBuffer source, long sourceOffset, IBuffer destination, long destinationOffset, long size);

    /// <summary>Copies the whole texture from rows padded to <see cref="PixelFormatExtensions.GetCopyRowPitch"/>.</summary>
    void CopyBufferToTexture(IBuffer source, long sourceOffset, ITexture destination);

    /// <summary>Copies a whole mip level of the texture into rows padded to <see cref="PixelFormatExtensions.GetCopyRowPitch"/>.</summary>
    void CopyTextureToBuffer(ITexture source, IBuffer destination, long destinationOffset, int mipLevel = 0);

    void BeginRendering(ReadOnlySpan<ColorTarget> colorTargets, DepthTarget? depthTarget = null);

    void EndRendering();

    void SetViewport(in Viewport viewport);

    void SetScissor(int x, int y, int width, int height);

    void SetPipeline(IPipeline pipeline);

    void SetIndexBuffer(IBuffer buffer, IndexFormat format, long offset = 0);

    /// <summary>Sets the push constants the next draws read: a multiple of 4 bytes, at most <see cref="GraphicsLimits.PushConstantSize"/>.</summary>
    void PushConstants<T>(in T data)
        where T : unmanaged;

    void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0);

    /// <summary>Runs the compute pipeline set last on the given number of thread groups.</summary>
    void Dispatch(int groupsX, int groupsY = 1, int groupsZ = 1);

    void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int vertexOffset = 0, int firstInstance = 0);
}

