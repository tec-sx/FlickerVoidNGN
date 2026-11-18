namespace FlickerVoid.Engine.Graphics;

/// <summary>Which triangles are skipped; front faces wind clockwise on screen, as in Direct3D.</summary>
public enum CullMode : byte
{
    None,
    Front,
    Back,
}
