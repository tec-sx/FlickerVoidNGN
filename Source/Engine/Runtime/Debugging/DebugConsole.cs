using System.Text;

namespace FlickerVoid.Engine.Runtime.Debugging;

/// <summary>A debug console with a built-in "help" command that lists the entries.</summary>
public sealed class DebugConsole : IDebugConsole
{
    private readonly Dictionary<string, ConsoleEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public DebugConsole()
    {
        ConsoleCommand help = new("help", "Lists the commands and variables, or those starting with the given text.", ListEntries);
        Add(help);
    }

    public IReadOnlyCollection<ConsoleEntry> Entries => _entries.Values;

    public void Add(ConsoleEntry entry)
    {
        if (!_entries.TryAdd(entry.Name, entry))
        {
            throw new ArgumentException($"A console entry named '{entry.Name}' is already registered.", nameof(entry));
        }
    }

    public string Execute(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return "";
        }

        string name = trimmed;
        string arguments = "";
        int space = trimmed.IndexOf(' ');
        if (space >= 0)
        {
            name = trimmed[..space];
            arguments = trimmed[(space + 1)..].Trim();
        }

        if (!_entries.TryGetValue(name, out ConsoleEntry? entry))
        {
            return $"Unknown command '{name}'. Type help for a list.";
        }

        return entry.Execute(arguments);
    }

    private string ListEntries(string[] words)
    {
        string prefix = "";
        if (words.Length > 0)
        {
            prefix = words[0];
        }

        IEnumerable<ConsoleEntry> matching = _entries.Values.Where(entry => entry.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        IOrderedEnumerable<ConsoleEntry> sorted = matching.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
        StringBuilder list = new();
        foreach (ConsoleEntry entry in sorted)
        {
            list.AppendLine($"{entry.Name}  {entry.Help}");
        }

        return list.ToString().TrimEnd();
    }
}
