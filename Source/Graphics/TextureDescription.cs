namespace FlickerVoid.Engine.Graphics;

/// <summary>Size, format and use of a 2D texture.</summary>
public readonly record struct TextureDescription(int Width, int Height, PixelFormat Format, TextureUsage Usage)
{
    /// <summary>Shown in debuggers and validation messages.</summary>
    public string? Name { get; init; }
}
