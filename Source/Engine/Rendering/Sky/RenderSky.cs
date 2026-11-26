namespace FlickerVoid.Engine.Rendering.Sky;

/// <summary>The painted sky a scene names: its mask image and which way north lies in the world, in degrees.</summary>
public sealed record RenderSky(string Mask, float North);
