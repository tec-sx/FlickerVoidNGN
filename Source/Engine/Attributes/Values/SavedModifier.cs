namespace FlickerVoid.Engine.Attributes.Values;

/// <summary>A modifier on an entity as a save keeps it: its handle, so whoever applied it can still take it off, and its file.</summary>
public sealed class SavedModifier
{
    public Guid Handle { get; set; }

    public string Modifier { get; set; } = string.Empty;
}
