namespace FlickerVoid.Engine.Interaction.Interactors;

/// <summary>How an interaction ended.</summary>
public enum InteractionEnd : byte
{
    /// <summary>Its gesture completed and its action was done.</summary>
    Succeeded,

    /// <summary>The interactor focused on something else.</summary>
    FocusLost,

    /// <summary>The interactor moved out of reach.</summary>
    OutOfReach,

    /// <summary>The offer's conditions stopped holding, or it was used up.</summary>
    RequirementFailed,

    /// <summary>A hold was released too early.</summary>
    Released,

    /// <summary>A mash ran out of time.</summary>
    TimedOut,

    /// <summary>The input was cancelled, or the interactor stopped.</summary>
    Cancelled,

    /// <summary>The interactable stopped or was destroyed.</summary>
    Removed,
}
