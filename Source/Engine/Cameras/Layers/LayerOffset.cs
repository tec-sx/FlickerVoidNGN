using System.Numerics;

namespace FlickerVoid.Engine.Cameras.Layers;

/// <summary>The running state of a layer that eases an offset across the camera's view.</summary>
public sealed class LayerOffset
{
    public Vector3 Offset { get; set; }
}
