namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>
/// Marks an entity whose meshes are drawn black and white, without outlines, and whose doorways aren't drawn, as the editor's creator
/// greys the rest of a level; never saved.
/// </summary>
public readonly record struct Greyed;
