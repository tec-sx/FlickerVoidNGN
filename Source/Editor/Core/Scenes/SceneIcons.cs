using Arch.Core;

using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>The outline's icon of an entity, from the first component it has that names one; <c>Empty</c> when none does.</summary>
public static class SceneIcons
{
    /// <summary>Scene component names and their icons, most telling first: a character with a mesh is a character.</summary>
    private static readonly (string Component, string Icon)[] ByComponent =
    [
        ("camera", "Camera"),
        ("protagonist", "Character"),
        ("characterMover", "Character"),
        ("pointLight", "PointLight"),
        ("spotLight", "SpotLight"),
        ("irradianceVolume", "Probe"),
        ("sceneMood", "Mood"),
        ("sceneEnvironment", "Environment"),
        ("doorSwing", "Door"),
        ("doorway", "Door"),
        ("lock", "Lock"),
        ("pickup", "Item"),
        ("triggerArea", "Trigger"),
        ("indoorVolume", "Layers"),
        ("interactable", "Interact"),
        ("vehicle", "Vehicle"),
        ("meshRenderer", "Model"),
        ("greybox", "Cube"),
    ];

    public const string Folder = "Folder";

    public static string For(World world, Entity entity, IReadOnlyList<SceneComponent> components)
    {
        foreach ((string name, string icon) in ByComponent)
        {
            SceneComponent? component = components.FirstOrDefault(component => component.Name == name);
            if (component is not null && component.Has(world, entity))
            {
                return icon;
            }
        }

        return "Empty";
    }
}
