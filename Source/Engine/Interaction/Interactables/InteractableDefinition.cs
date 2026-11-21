using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Interaction.Interactables;

/// <summary>What can be done with a kind of interactable (a door, a lever, a chest), by whom, and how long it rests between uses.</summary>
public sealed class InteractableDefinition : Definition
{
    /// <summary>At most three; offers sharing an input are tried heaviest first, so one click unlocks a locked door and opens an unlocked one.</summary>
    public List<InteractionOffer> Offers { get; set; } = [];

    /// <summary>Whether the interface lists the offers while it is focused.</summary>
    public bool ShowOffers { get; set; } = true;

    /// <summary>Seconds it rests after a use before it can be used again.</summary>
    public float Cooldown { get; set; }

    /// <summary>The kinds of interactor that may use it (<see cref="Interactors.Interactor.Kind"/>, or a parent tag of it); empty lets any use it.</summary>
    public List<Tag> CompatibleInteractors { get; set; } = [];

    /// <summary>Whether an interactor of <paramref name="kind"/> may use it.</summary>
    public bool Accepts(Tag kind)
    {
        if (CompatibleInteractors.Count == 0)
        {
            return true;
        }

        foreach (Tag compatible in CompatibleInteractors)
        {
            if (kind.MatchesTag(compatible))
            {
                return true;
            }
        }

        return false;
    }
}
