namespace FlickerVoid.Engine.Runtime.Debugging;

/// <summary>A console command: runs with the typed words after its name and returns what to show.</summary>
public sealed class ConsoleCommand(string name, string help, Func<string[], string> run)
    : ConsoleEntry(name, help)
{
    public override string Execute(string arguments)
    {
        string[] words = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return run(words);
    }
}
