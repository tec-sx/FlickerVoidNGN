namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>A model a mesh name was made into, and the content file it was made from (null when the name alone makes it), so it is made again when that file changes.</summary>
public sealed record MadeMesh(ModelData Model, string? File);
