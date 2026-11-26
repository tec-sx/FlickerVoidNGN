using FlickerVoid.Engine.Runtime.Debugging;

namespace FlickerVoid.Engine.Runtime.Tests.Settings;

/// <summary>Builds the debug console whose variables a settings file reads and writes.</summary>
internal sealed class SettingsFileTestsBuilder
{
    private readonly List<ConsoleVariable> _variables = [];

    public SettingsFileTestsBuilder WithVariable(ConsoleVariable variable)
    {
        _variables.Add(variable);
        return this;
    }

    public IDebugConsole Build()
    {
        DebugConsole console = new();
        foreach (ConsoleVariable variable in _variables)
        {
            console.Add(variable);
        }

        return console;
    }
}
