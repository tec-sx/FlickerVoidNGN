using FlickerVoid.Editor.Core.Menus;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>The Add Component menu's choices: each component under its module, with an icon for what it is.</summary>
public static class ComponentChoices
{
    /// <summary>Icons for components that say what they are, else their module's.</summary>
    private static readonly Dictionary<string, string> ByComponent = new()
    {
        ["camera"] = "Camera",
        ["pointLight"] = "PointLight",
        ["spotLight"] = "SpotLight",
        ["irradianceVolume"] = "Probe",
        ["sceneMood"] = "Mood",
        ["sceneEnvironment"] = "Environment",
        ["meshRenderer"] = "Look",
        ["greybox"] = "Cube",
        ["protagonist"] = "Character",
        ["transform"] = "Transform",
        ["interactable"] = "Talk",
        ["scannable"] = "Scan",
        ["mapMarker"] = "Pin",
        ["perception"] = "Visible",
        ["outline"] = "Outline",
        ["vehicle"] = "Vehicle",
        ["building"] = "Building",
        ["foliage"] = "Tree",
        ["foliageScatter"] = "Tree",
    };

    private static readonly Dictionary<string, string> ByModule = new()
    {
        ["Rendering"] = "Look",
        ["Cameras"] = "Camera",
        ["Characters"] = "Behaviour",
        ["Ai"] = "Behaviour",
        ["Gameplay"] = "Behaviour",
        ["Physics"] = "Physics",
        ["Interaction"] = "Interact",
        ["Inventory"] = "Inventory",
        ["Attributes"] = "Attributes",
        ["Abilities"] = "Attributes",
        ["Story"] = "Story",
        ["Dialogue"] = "Story",
        ["Flow"] = "Flow",
        ["Maps"] = "Pin",
        ["Runtime"] = "Transform",
        ["Vehicles"] = "Vehicle",
        ["Buildings"] = "Building",
        ["Foliage"] = "Tree",
        ["Creations"] = "Cube",
    };

    /// <summary>The icon of everything: the inspector rail's All.</summary>
    public const string AllIcon = "Layers";

    private const string OtherIcon = "Entity";

    public static IReadOnlyList<MenuChoice> Build(IEnumerable<SceneComponent> components, Action<SceneComponent> add)
    {
        List<MenuChoice> choices = [];
        foreach (SceneComponent component in components)
        {
            Type type = component.TypeInfo.Type;
            string name = DisplayNames.FromIdentifier(type.Name);
            string group = GetModule(type);
            choices.Add(new MenuChoice(name, () => add(component), group, GetIcon(component.Name, group)));
        }

        return [.. choices.OrderBy(choice => choice.Group, StringComparer.Ordinal).ThenBy(choice => choice.Name, StringComparer.Ordinal)];
    }

    /// <summary>The module from the namespace: Rendering for <c>FlickerVoid.Engine.Rendering.Lighting</c>, Gameplay for <c>Bleeders.Gameplay.Doors</c>.</summary>
    internal static string GetModule(Type type)
    {
        string[] parts = (type.Namespace ?? "").Split('.');
        if (parts.Length >= 3 && parts[0] == "FlickerVoid" && parts[1] == "Engine")
        {
            return parts[2];
        }

        if (parts.Length >= 2)
        {
            return parts[1];
        }

        return "Other";
    }

    /// <summary>The icon of a module on the inspector's category rail.</summary>
    public static string GetModuleIcon(string module) => ByModule.GetValueOrDefault(module, OtherIcon);

    /// <summary>The icon of a component: its own when it says what it is, else its module's.</summary>
    internal static string GetIcon(string component, string module)
    {
        if (ByComponent.TryGetValue(component, out string? icon))
        {
            return icon;
        }

        return GetModuleIcon(module);
    }
}
