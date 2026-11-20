namespace FlickerVoid.Engine.Gameplay.Tags;

/// <summary>A hierarchical, dot-separated name (Fact.Social.Fame) that identifies and classifies; compared ignoring case.</summary>
public readonly struct Tag : IEquatable<Tag>
{
    private readonly string? _name;

    public Tag(string name)
    {
        _name = name;
    }

    public static Tag None => default;

    public string Name => _name ?? string.Empty;

    public bool IsValid => !string.IsNullOrEmpty(_name);

    /// <summary>True for this tag and for any tag below it: Fact.Social.Fame matches Fact.Social and Fact.Social.Fame.</summary>
    public bool MatchesTag(Tag parent)
    {
        if (!IsValid || !parent.IsValid)
        {
            return false;
        }

        string name = Name;
        string parentName = parent.Name;
        if (name.Length == parentName.Length)
        {
            return string.Equals(name, parentName, StringComparison.OrdinalIgnoreCase);
        }

        return name.Length > parentName.Length
            && name[parentName.Length] == '.'
            && name.StartsWith(parentName, StringComparison.OrdinalIgnoreCase);
    }

    public bool Equals(Tag other) => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is Tag other && Equals(other);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    public override string ToString() => Name;

    public static bool operator ==(Tag left, Tag right) => left.Equals(right);

    public static bool operator !=(Tag left, Tag right) => !left.Equals(right);
}
