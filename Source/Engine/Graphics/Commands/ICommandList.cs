using FlickerVoid.Engine.Graphics.AccelerationStructures;
using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Devices;
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

    /// <summary>Ends recording; throws when a timer is still open.</summary>
    void End();

    /// <summary>
    /// The GPU times of the timers recorded in the list's last submission, in the order they began, read when <see cref="Begin"/>
    /// has waited for it, so reading them never stalls; empty before the first submission and without
    /// <see cref="GraphicsFeatures.Timestamps"/>.
    /// </summary>
    IReadOnlyList<GpuTiming> Timings { get; }

    /// <summary>
    /// Starts timing the GPU work recorded next under the given name, inside the timer that is open, if any; at most
    /// <see cref="GraphicsLimits.TimersPerCommandList"/> per recording.
    /// </summary>
    void BeginTimer(string name);

    /// <summary>Ends the timer begun last; throws when none is open.</summary>
    void EndTimer();

    void Barrier(ITexture texture, ResourceState state);

    void Barrier(IBuffer buffer, ResourceState state);

    void CopyBuffer(IBuffer source, long sourceOffset, IBuffer destination, long destinationOffset, long size);

    /// <summary>Copies a whole mip level of the texture from rows padded to <see cref="PixelFormatExtensions.GetCopyRowPitch"/>.</summary>
    void CopyBufferToTexture(IBuffer source, long sourceOffset, ITexture destination, int mipLevel = 0);

    /// <summary>Copies a whole mip level of the texture into rows padded to <see cref="PixelFormatExtensions.GetCopyRowPitch"/>.</summary>
    void CopyTextureToBuffer(ITexture source, IBuffer destination, long destinationOffset, int mipLevel = 0);

    /// <summary>
    /// Copies the top level of a texture into another at least as large whose format has the same bytes per pixel, such as an sRGB
    /// texture into its UNorm twin, with its top left corner at the given pixel; the bytes are copied as they are. The source must be
    /// in the CopySource state and the destination in CopyDestination.
    /// </summary>
    void CopyTexture(ITexture source, ITexture destination, int destinationX = 0, int destinationY = 0);

    /// <summary>
    /// Copies a rectangle of a texture's top level to the same place in another of the same format, such as a tile of a shadow
    /// atlas; the source must be in the CopySource state and the destination in CopyDestination.
    /// </summary>
    void CopyTextureRegion(ITexture source, ITexture destination, int x, int y, int width, int height);

    void BeginRendering(ReadOnlySpan<ColorTarget> colorTargets, DepthTarget? depthTarget = null);

    void EndRendering();

    /// <summary>Clears a rectangle of the depth target being drawn to, leaving the rest as it is.</summary>
    void ClearDepth(int x, int y, int width, int height, float depth = 1);

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

    /// <summary>
    /// Draws as <see cref="DrawIndexed"/> with the five words at the offset of a buffer with <see cref="BufferUsage.Indirect"/>:
    /// index count, instance count, first index, vertex offset and first instance, which a shader may have written before a
    /// barrier to <see cref="ResourceState.IndirectArgument"/>.
    /// </summary>
    void DrawIndexedIndirect(IBuffer arguments, long offset = 0);

    /// <summary>
    /// Builds bottom-level acceleration structures from their geometry, or refits refittable ones built before to their geometry's
    /// new vertex positions, all at once. Builds wait for every earlier command and leave the structures readable by later builds
    /// and by shaders' ray queries, so they need no barriers.
    /// </summary>
    void BuildBottomLevels(ReadOnlySpan<BottomLevelBuild> builds);

    /// <summary>
    /// Builds a top-level acceleration structure from the first <paramref name="instanceCount"/> <see cref="AccelerationStructureInstance"/>s
    /// of the buffer, which has <see cref="BufferUsage.AccelerationStructureInput"/>; the bottom-level structures they place must be
    /// built. Like bottom-level builds, it needs no barriers.
    /// </summary>
    void BuildTopLevel(IAccelerationStructure structure, IBuffer instances, int instanceCount);
}

