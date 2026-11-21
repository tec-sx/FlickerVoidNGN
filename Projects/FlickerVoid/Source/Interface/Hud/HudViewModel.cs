using System.Collections.ObjectModel;

using FlickerVoid.Engine.Dialogue.Conversations;
using FlickerVoid.Engine.Interaction.Interactors;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Story.Quests;
using FlickerVoid.Interface.Hud.Conversations;
using FlickerVoid.Interface.Hud.Minimap;
using FlickerVoid.Interface.Hud.Prompts;
using FlickerVoid.Interface.Hud.Quests;

namespace FlickerVoid.Interface.Hud;

/// <summary>
/// The HUD in its three places: information at the top left (the tracked quests), the action at the bottom centre (the
/// conversation's dialogue box, or else the prompt for what the pointer is over) and navigation at the bottom right (the minimap).
/// </summary>
public sealed class HudViewModel
{
    public HudViewModel(Game game)
    {
        QuestSystem quests = game.Services.GetService<QuestSystem>()!;
        DialogueSystem dialogue = game.Services.GetService<DialogueSystem>()!;
        InteractionSystem interaction = game.Services.GetService<InteractionSystem>()!;
        Tracker = new QuestTrackerViewModel(quests, game.Events);
        Dialogue = new DialogueBoxViewModel(dialogue, game.World, game.Events);
        Prompt = new InteractionPromptViewModel(interaction, dialogue, game.World);
        Minimap = new MinimapViewModel(game.World);
    }

    public QuestTrackerViewModel Tracker { get; }

    public ObservableCollection<TrackedQuestViewModel> Quests => Tracker.Quests;

    public DialogueBoxViewModel Dialogue { get; }

    public InteractionPromptViewModel Prompt { get; }

    public MinimapViewModel Minimap { get; }
}
