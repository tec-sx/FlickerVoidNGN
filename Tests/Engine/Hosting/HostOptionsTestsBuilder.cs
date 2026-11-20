namespace FlickerVoid.Engine.Hosting.Tests;

/// <summary>Builds host options from a command line and a startup scene.</summary>
internal sealed class HostOptionsTestsBuilder
{
    private string[] _arguments = [];
    private string? _startupScene;

    public HostOptionsTestsBuilder WithArguments(params string[] arguments)
    {
        _arguments = arguments;
        return this;
    }

    public HostOptionsTestsBuilder WithStartupScene(string scene)
    {
        _startupScene = scene;
        return this;
    }

    public HostOptions Build() => HostOptions.Parse(_arguments, _startupScene);
}
