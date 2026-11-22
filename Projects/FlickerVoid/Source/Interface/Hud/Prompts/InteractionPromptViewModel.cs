using System.Collections.ObjectModel;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Dialogue.Conversations;
using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Interaction.Interactors;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Interface.Hud.Prompts;

/// <summary>
/// The HUD's interaction prompt: the name and offers of the interactable under the pointer, as the protagonist sees them;
/// hidden away from interactables and while the protagonist talks.
/// </summary>
public sealed partial class InteractionPromptViewModel(InteractionSystem interaction, DialogueSystem dialogue, World world)
    : ObservableObject
{
    private static readonly QueryDescription Protagonists = new QueryDescription().WithAll<Gameplay.Protagonist.Protagonist, Interactor>();

    private Entity _shown = Entity.Null;
    private int _version = -1;

    [ObservableProperty]
    public partial bool IsVisible { get; private set; }

    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    public ObservableCollection<PromptOfferViewModel> Offers { get; } = [];

    /// <summary>Shows the offers of what the pointer is over, or hides the prompt; offers are listed again when the target or its uses change.</summary>
    public void Update(Entity hovered)
    {
        Entity protagonist = FindProtagonist();
        bool show = protagonist != Entity.Null && !dialogue.IsInConversation && hovered != Entity.Null && world.IsAlive(hovered)
            && world.Has<ActiveInteractable>(hovered);
        if (!show)
        {
            Hide();
            return;
        }

        int version = Version(hovered);
        if (hovered == _shown && version == _version)
        {
            return;
        }

        _shown = hovered;
        _version = version;
        Title = FindTitle(hovered);
        Offers.Clear();
        IReadOnlyList<OfferView> offers = interaction.GetOffers(protagonist, hovered);
        foreach (OfferView offer in offers)
        {
            Offers.Add(new PromptOfferViewModel(offer.Display.Name, offer.CanExecute, offer.LockedReason));
        }

        IsVisible = Offers.Count > 0;
    }

    private void Hide()
    {
        _shown = Entity.Null;
        _version = -1;
        IsVisible = false;
        Offers.Clear();
    }

    /// <summary>Changes when the interactable's state or uses change, so the prompt follows a door that unlocked or a pickup taken.</summary>
    private int Version(Entity hovered)
    {
        ActiveInteractable active = world.Get<ActiveInteractable>(hovered);
        HashCode hash = new();
        hash.Add(active.State);
        foreach (int uses in active.Uses)
        {
            hash.Add(uses);
        }

        return hash.ToHashCode();
    }

    private string FindTitle(Entity hovered)
    {
        if (world.TryGet(hovered, out EntityName name) && name.Value.Length > 0)
        {
            return name.Value;
        }

        return world.Get<ActiveInteractable>(hovered).Definition.Display.Name;
    }

    private Entity FindProtagonist()
    {
        Entity found = Entity.Null;
        world.Query(in Protagonists, (Entity entity) =>
        {
            if (found == Entity.Null)
            {
                found = entity;
            }
        });
        return found;
    }
}
