namespace FlickerVoid.Engine.Cameras.Layers;

/// <summary>
/// A small, bounded offset on top of a shot's pose, without owning the camera; polymorphic data named by <c>$type</c>
/// (<c>RegisterDerived&lt;CameraLayer, T&gt;</c>). Shared and read-only: what changes while it runs is its state, which the
/// director keeps per shot. Derive from <see cref="CameraLayer{TState}"/>.
/// </summary>
public abstract class CameraLayer
{
    internal abstract object CreateState();

    internal abstract CameraOffset Update(object state, CameraLayerFrame frame);
}
