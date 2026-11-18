namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>A scene file: the entities it spawns.</summary>
public sealed class SceneData
{
    public List<EntityData> Entities { get; set; } = [];
}
