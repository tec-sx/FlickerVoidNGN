using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Interaction.Interactables;

/// <summary>Something interactors can use, as its definition says; while the game plays it is also an <see cref="ActiveInteractable"/>.</summary>
public struct Interactable
{
    public Interactable()
    {
    }

    /// <summary>The <see cref="InteractableDefinition"/> data asset.</summary>
    [ContentFile(".fvdata")]
    public string Definition { get; set; } = string.Empty;
}
