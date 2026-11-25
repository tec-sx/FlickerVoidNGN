namespace FlickerVoid.Editor.Assets;

/// <summary>
/// A kind of content file as the editor knows it: the name its filter shows, the extensions that mark it, its icon (a
/// <c>MaterialIcons</c> name) and colour in the browser, and what New writes of it, if anything. <see cref="AssetTypes"/> lists them.
/// </summary>
public sealed class AssetType(string name, string icon, string color, params string[] extensions)
{
    public string Name { get; } = name;

    public string Icon { get; } = icon;

    /// <summary>The colour as <c>#RRGGBB</c>.</summary>
    public string Color { get; } = color;

    public IReadOnlyList<string> Extensions { get; } = extensions;

    /// <summary>What New writes of this type; null when New doesn't make it.</summary>
    public AssetNew? New { get; init; }

    /// <summary>Whether the path ends in one of the type's extensions, ignoring case.</summary>
    public bool Marks(string path)
    {
        foreach (string extension in Extensions)
        {
            if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public override string ToString() => Name;
}
