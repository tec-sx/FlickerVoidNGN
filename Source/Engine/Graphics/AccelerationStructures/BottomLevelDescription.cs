namespace FlickerVoid.Engine.Graphics.AccelerationStructures;

/// <summary>
/// The size of the triangles a bottom-level acceleration structure holds, and whether it can be refitted to new vertex positions,
/// as posed meshes are each frame.
/// </summary>
public readonly record struct BottomLevelDescription(int VertexCount, int IndexCount, bool Refittable = false)
{
    /// <summary>Shown in debuggers and validation messages.</summary>
    public string? Name { get; init; }
}
