namespace FlickerVoid.Engine.Runtime.Debugging;

/// <summary>Runs typed lines as commands and shows or sets variables, for debugging; names ignore case.</summary>
public interface IDebugConsole
{
    IReadOnlyCollection<ConsoleEntry> Entries { get; }

    /// <exception cref="ArgumentException">An entry with the same name, ignoring case, is already registered.</exception>
    void Add(ConsoleEntry entry);

    /// <summary>Runs a typed line: the first word names the entry, the rest goes to it. Returns what to show.</summary>
    string Execute(string line);
}
