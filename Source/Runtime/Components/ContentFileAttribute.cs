namespace FlickerVoid.Engine.Runtime.Components;

/// <summary>Marks a string property (or each string of a list) as a file in the content folder with one of the extensions, so the editor suggests those files.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ContentFileAttribute(params string[] extensions) : Attribute
{
    public IReadOnlyList<string> Extensions { get; } = extensions;
}
