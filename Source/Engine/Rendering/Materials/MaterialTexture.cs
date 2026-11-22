namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// The bindless indices a material's texture parameter packs: its image, and the relief a painting's impasto was baked into
/// (<see cref="MaterialLayout.NoTexture"/> when it has none).
/// </summary>
public readonly record struct MaterialTexture(uint Image, uint Relief);
