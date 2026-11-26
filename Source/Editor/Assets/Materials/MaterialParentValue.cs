using System.Text.Json;

using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Assets.Materials;

/// <summary>The master or material the edited material is an instance of, picked among the content's materials and masters.</summary>
public sealed class MaterialParentValue(MaterialDocument document) : IEditableValue
{
    private static readonly ContentFileAttribute ParentFiles = new(MaterialFile.Extension, MasterFile.Extension);

    public string Label => "Parent";

    public Type Type => typeof(string);

    public bool CanWrite => true;

    /// <summary>A material may have no parent, keeping the scene shader's defaults.</summary>
    public bool AcceptsNull => true;

    public TAttribute? GetAttribute<TAttribute>()
        where TAttribute : Attribute
    {
        if (typeof(TAttribute) == typeof(ContentFileAttribute))
        {
            return ParentFiles as TAttribute;
        }

        return null;
    }

    public object? Get() => document.Material.Parent;

    public void Set(object? value)
    {
        document.SetParent(value as string);
    }

    public JsonSerializerOptions? Options => null;

    public string ToJson(object? value) => value as string ?? "";
}
