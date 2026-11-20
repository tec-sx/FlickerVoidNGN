namespace FlickerVoid.Engine.Rendering.Pipeline;

/// <summary>
/// One stage of drawing a frame. Every pass prepares on the CPU first, then the frame's data is uploaded, then every pass
/// records its GPU work in order.
/// </summary>
public interface IRenderPass : IDisposable
{
    /// <summary>Works out what the pass needs this frame, before the frame's data is uploaded.</summary>
    void Prepare(RenderContext context);

    /// <summary>Records the pass's GPU work.</summary>
    void Execute(RenderContext context);
}
