using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Interaction.Interactors;

/// <summary>Something that uses interactables, usually the protagonist: what kind it is, and how close it must stand to use something.</summary>
public struct Interactor
{
    public Interactor()
    {
    }

    /// <summary>Matched against an interactable's compatible interactors, such as <c>Interactor.Player</c>.</summary>
    public Tag Kind { get; set; }

    /// <summary>Metres from an interactable within which it can use it.</summary>
    public float Reach { get; set; } = 1.5f;
}
