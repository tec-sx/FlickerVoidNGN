namespace FlickerVoid.Engine.Cameras.Layers;

/// <summary>A camera layer whose running state is a <typeparamref name="TState"/>, made new for each shot it runs on.</summary>
public abstract class CameraLayer<TState> : CameraLayer
    where TState : class, new()
{
    internal sealed override object CreateState() => new TState();

    internal sealed override CameraOffset Update(object state, CameraLayerFrame frame) => Update((TState)state, frame);

    /// <summary>The layer's offset this frame, within its own limits.</summary>
    protected abstract CameraOffset Update(TState state, CameraLayerFrame frame);
}
