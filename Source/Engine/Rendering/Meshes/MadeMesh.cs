namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>A model a mesh name was made into, and the content file it was made from (null when the name alone makes it), so it is made again when that file changes.</summary>
public sealed record MadeMesh(ModelData Model, string? File)
{
    /// <summary>More content files the model was made from, or would be if they were there; it is made again when one changes too.</summary>
    public IReadOnlyList<string> OtherFiles { get; init; } = [];
}
