using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Interface;

/// <summary>The names the interface shows for tags and definitions that may have no display name.</summary>
public static class DisplayNames
{
    /// <summary>The tag without its parents: Character.Innkeeper is "Innkeeper".</summary>
    public static string Leaf(Tag tag)
    {
        string name = tag.Name;
        int last = name.LastIndexOf('.');
        return name[(last + 1)..];
    }

    /// <summary>The definition's display name, else the leaf of its id.</summary>
    public static string Of(Definition definition)
    {
        if (definition.Display.Name.Length > 0)
        {
            return definition.Display.Name;
        }

        return Leaf(definition.Id);
    }
}
