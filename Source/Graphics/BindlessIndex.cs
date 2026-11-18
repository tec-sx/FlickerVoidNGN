namespace FlickerVoid.Engine.Graphics;

/// <summary>Bindless slots shaders read resources through. Slot 0 of each table holds a placeholder, so a missing resource reads as zero instead of crashing.</summary>
public static class BindlessIndex
{
    /// <summary>The placeholder's slot, also given to resources shaders can't read.</summary>
    public const uint None = 0;
}
