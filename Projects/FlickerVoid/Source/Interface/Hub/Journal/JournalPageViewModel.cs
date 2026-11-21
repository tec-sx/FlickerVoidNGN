using System.Collections.ObjectModel;

using FlickerVoid.Engine.Story.Knowledge;
using FlickerVoid.Engine.Story.Quests;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Interface.Hub.Journal;

/// <summary>The hub's journal in three sections: active quests with their visible objectives, finished quests, and knowledge.</summary>
public sealed class JournalPageViewModel : HubPageViewModel
{
    private readonly QuestSystem _quests;
    private readonly KnowledgeSystem _knowledge;

    public JournalPageViewModel(Game game)
        : base("Journal")
    {
        _quests = game.Services.GetService<QuestSystem>()!;
        _knowledge = game.Services.GetService<KnowledgeSystem>()!;
        game.Events.Handle((in QuestChangedEvent _) => RefreshFor(Owner));
    }

    public ObservableCollection<JournalQuestViewModel> ActiveQuests { get; } = [];

    /// <summary>Completed and failed quests.</summary>
    public ObservableCollection<JournalEntryViewModel> FinishedQuests { get; } = [];

    public ObservableCollection<JournalEntryViewModel> Knowledge { get; } = [];

    protected override void Refresh()
    {
        ActiveQuests.Clear();
        foreach (QuestDefinition quest in _quests.GetQuests(QuestState.Active))
        {
            List<string> objectives = [];
            foreach (QuestObjective objective in quest.Objectives)
            {
                if (_quests.IsObjectiveVisible(objective))
                {
                    objectives.Add(objective.Description);
                }
            }

            ActiveQuests.Add(new JournalQuestViewModel(DisplayNames.Of(quest), quest.Display.ShortDescription, objectives));
        }

        FinishedQuests.Clear();
        AddFinished(QuestState.Completed, "Completed");
        AddFinished(QuestState.Failed, "Failed");

        Knowledge.Clear();
        foreach (KnowledgeDefinition known in _knowledge.GetKnown())
        {
            Knowledge.Add(new JournalEntryViewModel(DisplayNames.Of(known), known.Display.ShortDescription));
        }
    }

    private void AddFinished(QuestState state, string label)
    {
        foreach (QuestDefinition quest in _quests.GetQuests(state))
        {
            FinishedQuests.Add(new JournalEntryViewModel(DisplayNames.Of(quest), label));
        }
    }
}
