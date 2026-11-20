namespace FlickerVoid.Engine.Graphics.Devices;

/// <summary>The machine has no GPU or driver that can run the requested graphics API.</summary>
public sealed class GraphicsUnavailableException(string message) : Exception(message);

