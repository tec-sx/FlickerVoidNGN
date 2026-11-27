using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Localization;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Platform.Input.Gestures;

namespace FlickerVoid.Engine.Interaction.Interactors;

/// <summary>An offer as an interactor sees it now, for prompts: whether its conditions hold, the reason shown when they don't, and its uses left.</summary>
public sealed record OfferView(Tag Input, Tag Action, Gesture Gesture, DisplayInfo Display, int Weight, int Uses, bool Available, LocalizedText LockedReason)
{
    public bool CanExecute => Available && Uses != 0;
}
