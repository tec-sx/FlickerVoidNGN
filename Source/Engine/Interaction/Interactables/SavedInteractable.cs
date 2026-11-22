namespace FlickerVoid.Engine.Interaction.Interactables;

/// <summary>What a save keeps of an interactable: its state, the uses its offers have left and its cooldown left.</summary>
public sealed class SavedInteractable
{
    public InteractableState State { get; set; }

    public int[] Uses { get; set; } = [];

    public float Cooldown { get; set; }
}
