using System.Text.Json;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>A value a field edits, such as a component's property or a material's parameter.</summary>
public interface IEditableValue
{
    string Label { get; }

    Type Type { get; }

    bool CanWrite { get; }

    /// <summary>Whether the value may be set to null, such as an optional file name.</summary>
    bool AcceptsNull { get; }

    /// <summary>The attribute that refines how the value is edited, such as a range or a colour, if it has one.</summary>
    TAttribute? GetAttribute<TAttribute>()
        where TAttribute : Attribute;

    object? Get();

    void Set(object? value);

    /// <summary>The options whose metadata describes the value's type, so nested objects and lists get fields of their own; null for a plain value.</summary>
    JsonSerializerOptions? Options { get; }

    /// <summary>The value as it would be written in a content file.</summary>
    string ToJson(object? value);
}
