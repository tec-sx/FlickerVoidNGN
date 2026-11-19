namespace FlickerVoid.Engine.Rendering;

/// <summary>Triangles of a model that share a material, in the model's space and wound clockwise seen from the front.</summary>
public sealed record MeshPart(MeshVertex[] Vertices, uint[] Indices, MaterialData Material);
