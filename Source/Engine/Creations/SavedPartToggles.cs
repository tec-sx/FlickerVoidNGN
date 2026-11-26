namespace FlickerVoid.Engine.Creations;

/// <summary>What a save keeps of a saveable creation's parts: for each part with a toggle, by id, whether it is open.</summary>
public sealed class SavedPartToggles
{
    public Dictionary<string, bool> Open { get; set; } = [];
}
