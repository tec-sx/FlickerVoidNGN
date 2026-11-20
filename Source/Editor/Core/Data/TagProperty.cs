using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Editor.Core.Data;

/// <summary>A tag property edited as its name, so the inspector's text field edits it.</summary>
public sealed class TagProperty(IEditableValue tag) : IEditableValue
{
    public string Label => tag.Label;

    public Type Type => typeof(string);

    public bool CanWrite => tag.CanWrite;

    public bool AcceptsNull => false;

    public TAttribute? GetAttribute<TAttribute>()
        where TAttribute : Attribute
    {
        return tag.GetAttribute<TAttribute>();
    }

    public object? Get()
    {
        Tag value = (Tag)tag.Get()!;
        return value.Name;
    }

    public void Set(object? value)
    {
        string name = value as string ?? "";
        Tag next = Tag.None;
        if (name.Length > 0)
        {
            next = new Tag(name);
        }

        tag.Set(next);
    }

    public string ToJson(object? value) => tag.ToJson(value);
}
