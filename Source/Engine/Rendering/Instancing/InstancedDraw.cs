using FlickerVoid.Engine.Graphics.Buffers;

namespace FlickerVoid.Engine.Rendering.Instancing;

/// <summary>
/// What a batch's part draws with this frame: the batch, its copies on the GPU for this frame, the list of those the view's cull
/// kept, and the indirect arguments the cull writes for the part, which is the model's <paramref name="Part"/>th.
/// </summary>
internal sealed record InstancedDraw(InstanceBatch Batch, IBuffer Copies, IBuffer Visible, IBuffer Arguments, int Part)
{
    /// <summary>Where the part's five words of arguments start: after the cull's count, five words a part.</summary>
    public long ArgumentsOffset => BatchBuffers.ArgumentsOffset(Part);
}
