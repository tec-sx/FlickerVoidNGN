namespace FlickerVoid.Engine.Interaction.Interactables;

/// <summary>An interactable while the game plays: its loaded definition, its state, the uses its offers have left and the cooldown left.</summary>
public struct ActiveInteractable
{
    public InteractableDefinition Definition { get; set; }

    public InteractableState State { get; set; }

    /// <summary>Uses left per offer, in the definition's order; -1 is unlimited.</summary>
    public int[] Uses { get; set; }

    /// <summary>Seconds of cooldown left.</summary>
    public float Cooldown { get; set; }

    public readonly bool CanInteract => State == InteractableState.Awake;
}
