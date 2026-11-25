namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>
/// Marks an entity the renderer leaves out, as the editor's creator sets the rest of a level aside: its meshes and lights are not
/// drawn, as with <see cref="Hidden"/>, which stays the person's own choice; never saved.
/// </summary>
public readonly record struct Undrawn;
