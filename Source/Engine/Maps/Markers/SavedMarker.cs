namespace FlickerVoid.Engine.Maps.Markers;

/// <summary>What a save keeps of an entity's marker: whether it was discovered, for discovery markers without a fact of their own.</summary>
public sealed class SavedMarker
{
    public bool Discovered { get; set; }
}
