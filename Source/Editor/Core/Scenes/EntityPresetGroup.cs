namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>A submenu of the viewport's Add menu: the presets that share a group, in their given order.</summary>
public sealed record EntityPresetGroup(string Name, IReadOnlyList<EntityPreset> Presets)
{
    /// <summary>The group of presets that name none.</summary>
    public const string Custom = "Custom";

    public static IReadOnlyList<EntityPresetGroup> From(IEnumerable<EntityPreset> presets)
    {
        List<EntityPresetGroup> groups = [];
        foreach (IGrouping<string, EntityPreset> group in presets.GroupBy(preset => preset.Group ?? Custom))
        {
            EntityPreset[] members = [.. group];
            groups.Add(new EntityPresetGroup(group.Key, members));
        }

        return groups;
    }
}
