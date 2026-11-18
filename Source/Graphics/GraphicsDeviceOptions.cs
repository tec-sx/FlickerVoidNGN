namespace FlickerVoid.Engine.Graphics;

/// <summary>How a graphics device is created.</summary>
public sealed record GraphicsDeviceOptions
{
    /// <summary>Turns on the API's validation layer, which logs every misuse as an error. Slow; for development and tests.</summary>
    public bool Validation { get; init; }
}
