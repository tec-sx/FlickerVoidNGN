namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>How the content build stores a texture: BC7 blocks, a quarter of the memory, or every texel as it is.</summary>
public enum TextureCompression
{
    Bc7,
    None,
}
