namespace FlickerVoid.Engine.Story.Quests;

/// <summary>What a save keeps of the quests' flows: the ids of the active nodes of each running flow, by quest id.</summary>
public sealed class SavedQuestFlows
{
    public Dictionary<string, List<string>> Running { get; set; } = [];
}
