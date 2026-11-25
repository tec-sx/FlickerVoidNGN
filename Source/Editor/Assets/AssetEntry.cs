namespace FlickerVoid.Editor.Assets;

/// <summary>
/// A content file or folder: its path in the content folder with forward slashes, its name without extensions, and its type. A file
/// with the files it owns (<see cref="AssetCatalog"/>) is a group, which renames, moves and duplicates as one.
/// </summary>
public sealed record AssetEntry(string Path, string Name, AssetType Type)
{
    /// <summary>The files named after this one, which go with it.</summary>
    public IReadOnlyList<AssetEntry> Owned { get; init; } = [];

    /// <summary>The path of the file that owns this one; null when none does.</summary>
    public string? Owner { get; init; }

    /// <summary>Whether the browser lists the owned files after this one; left out of equality, so expanding keeps the selection.</summary>
    public bool IsExpanded { get; init; }

    /// <summary>Whether the file owns files or is owned, so its tile is framed.</summary>
    public bool IsGrouped => Owned.Count > 0 || Owner is not null;

    /// <summary>What the tile shows: the name, or an owned file's whole file name, as the files of a group share their name.</summary>
    public string Label => Owner is null ? Name : Path[(Path.LastIndexOf('/') + 1)..];

    public bool Equals(AssetEntry? other)
    {
        return other is not null && Path == other.Path && Name == other.Name && Type == other.Type && Owner == other.Owner
            && Owned.SequenceEqual(other.Owned);
    }

    public override int GetHashCode() => HashCode.Combine(Path, Name, Type, Owner);
}
