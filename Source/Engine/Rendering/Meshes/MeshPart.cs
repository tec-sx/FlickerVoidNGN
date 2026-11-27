using FlickerVoid.Engine.Rendering.Materials;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// Triangles of a model that share a material, in the model's space and wound clockwise seen from the front. Skinned parts
/// keep their vertices in the bind pose with the joints that move each one.
/// </summary>
public sealed record MeshPart(MeshVertex[] Vertices, uint[] Indices, MaterialData Material)
{
    /// <summary>One entry per vertex, or null for a rigid part.</summary>
    public SkinVertex[]? Skin { get; init; }

    /// <summary>
    /// The name of the glTF node the part was read from, then of each node it hangs under, innermost first; a node's <c>fv_part</c>
    /// property stands for its name. Empty for parts not read from glTF.
    /// </summary>
    public IReadOnlyList<string> Nodes { get; init; } = [];
}

