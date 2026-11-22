using Arch.Core;

using FlickerVoid.Engine.Attributes.Modifiers;
using FlickerVoid.Engine.Attributes.Values;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Attributes.Rules;

/// <summary>What the attribute conditions and effects find in their context: the attribute system, the entity they look at and the assets they name.</summary>
internal static class AttributeContext
{
    public static bool TryGetOwner(ConditionContext context, ContextEntity which, out AttributeSystem attributes, out Entity owner)
    {
        IEntityView? view = context.GetEntity(which);
        AttributeSystem? system = context.GetService<AttributeSystem>();
        if (system is null || view is not WorldEntityView worldView)
        {
            attributes = null!;
            owner = Entity.Null;
            return false;
        }

        attributes = system;
        owner = worldView.Entity;
        return true;
    }

    /// <summary>The data asset at <paramref name="path"/>; null when there are no data assets or it can't be read.</summary>
    public static T? Load<T>(ConditionContext context, string path)
        where T : class
    {
        DataAssets? data = context.GetService<DataAssets>();
        if (data is null || path.Length == 0)
        {
            return null;
        }

        try
        {
            return data.Load<T>(path);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return null;
        }
    }

    /// <summary>An attribute's display name for descriptions: the last part of its tag.</summary>
    public static string Name(Tag attribute)
    {
        string name = attribute.Name;
        int dot = name.LastIndexOf('.');
        return name[(dot + 1)..];
    }

    public static string FileName(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        if (name.Length == 0)
        {
            return "<none>";
        }

        return name;
    }

    public static ModifierDefinition? LoadModifier(ConditionContext context, string path) => Load<ModifierDefinition>(context, path);
}
