namespace FlickerVoid.Engine.Interaction.Interactables;

/// <summary>Where an interactable is in its use.</summary>
public enum InteractableState : byte
{
    /// <summary>Ready to be used.</summary>
    Awake,

    /// <summary>An interactor is using it.</summary>
    Interacting,

    /// <summary>Resting after a use, for its definition's cooldown.</summary>
    Cooldown,

    /// <summary>Every offer is used up; it can't be used again.</summary>
    Completed,
}
