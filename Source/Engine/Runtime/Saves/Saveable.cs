namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>Saves the entity's state under an id unique in its scene, so a save brings it back as it was, or destroyed when it was gone.</summary>
public struct Saveable()
{
    public string Id { get; set; } = string.Empty;
}
