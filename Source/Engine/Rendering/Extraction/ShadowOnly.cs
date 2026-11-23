namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>Marks an entity whose meshes cast shadows but are not seen, such as a roof cut away while the camera looks into the room under it.</summary>
public readonly record struct ShadowOnly;
