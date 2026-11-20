namespace FlickerVoid.Engine.Graphics.Resources;

/// <summary>A GPU object. Disposing it releases it once the GPU has finished every submitted command list.</summary>
public interface IGraphicsResource : IDisposable
{
    /// <summary>Slot shaders read it through; <see cref="Graphics.BindlessIndex.None"/> when shaders can't read it.</summary>
    uint BindlessIndex { get; }
}

