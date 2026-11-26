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
        ["irradianceVolume"] = "Grid",
        ["sceneMood"] = "Mood",
        ["sceneEnvironment"] = "Mood",
        ["meshRenderer"] = "Model",
        ["greybox"] = "Model",
        ["protagonist"] = "Character",
        ["transform"] = "Move",
    };

    private static readonly Dictionary<string, string> ByModule = new()
    {
        ["Rendering"] = "Materials",
        ["Cameras"] = "Camera",
        ["Characters"] = "Character",
        ["Ai"] = "Character",
        ["Physics"] = "Model",
        ["Interaction"] = "Entity",
        ["Inventory"] = "Data",
        ["Attributes"] = "Table",
        ["Abilities"] = "Table",
        ["Story"] = "Flow",
        ["Dialogue"] = "Flow",
        ["Flow"] = "Flow",
        ["Maps"] = "Grid",
        ["Runtime"] = "Empty",
    };

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

    private static string GetIcon(string component, string module)
    {
        if (ByComponent.TryGetValue(component, out string? icon))
        {
            return icon;
        }

        if (ByModule.TryGetValue(module, out string? moduleIcon))
        {
            return moduleIcon;
        }

        return "Entity";
    }
}
