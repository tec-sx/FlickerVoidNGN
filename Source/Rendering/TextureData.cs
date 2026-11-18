namespace FlickerVoid.Engine.Rendering;

/// <summary>A decoded image: rows of 4-byte RGBA pixels, top row first.</summary>
public sealed record TextureData(int Width, int Height, byte[] Rgba);
