namespace FlickerVoid.Interface.Hub.Journal;

/// <summary>A line of the journal with its description, such as something the protagonist knows.</summary>
public sealed record JournalEntryViewModel(string Name, string Description);
