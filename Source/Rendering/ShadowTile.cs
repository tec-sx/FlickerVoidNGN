namespace FlickerVoid.Engine.Rendering;

/// <summary>A square of the shadow atlas, in texels from its top left.</summary>
public readonly record struct ShadowTile(int X, int Y, int Size);
