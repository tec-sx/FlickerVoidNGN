namespace FlickerVoid.Engine.Runtime.Debugging;

/// <summary>A command or variable the debug console finds by name.</summary>
public abstract class ConsoleEntry(string name, string help)
{
    public string Name { get; } = name;

    public string Help { get; } = help;

    /// <summary>Runs the entry with the rest of the typed line and returns what to show.</summary>
    public abstract string Execute(string arguments);
}
