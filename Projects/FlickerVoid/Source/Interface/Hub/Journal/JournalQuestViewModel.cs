namespace FlickerVoid.Interface.Hub.Journal;

/// <summary>A quest in the journal with the objectives it shows.</summary>
public sealed record JournalQuestViewModel(string Name, string Description, IReadOnlyList<string> Objectives);
