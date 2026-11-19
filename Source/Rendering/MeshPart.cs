namespace FlickerVoid.Engine.Rendering;

/// <summary>
/// Triangles of a model that share a material, in the model's space and wound clockwise seen from the front. Skinned parts
/// keep their vertices in the bind pose with the joints that move each one.
/// </summary>
public sealed record MeshPart(MeshVertex[] Vertices, uint[] Indices, MaterialData Material)
{
    /// <summary>One entry per vertex, or null for a rigid part.</summary>
    public SkinVertex[]? Skin { get; init; }
}
