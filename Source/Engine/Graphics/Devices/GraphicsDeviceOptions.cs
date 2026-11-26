namespace FlickerVoid.Engine.Graphics.Devices;

/// <summary>How a graphics device is created.</summary>
public sealed record GraphicsDeviceOptions
{
    /// <summary>Turns on the API's validation layer, which logs every misuse as an error. Slow; for development and tests.</summary>
    public bool Validation { get; init; }

    /// <summary>The features to turn on where the GPU has them; leaving one out runs as on a GPU without it.</summary>
    public GraphicsFeatures Features { get; init; } = GraphicsFeatures.RayQueries;
}
