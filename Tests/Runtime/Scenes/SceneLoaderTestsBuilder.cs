using Arch.Core;

using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Engine.Runtime.Tests.Content;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Runtime.Tests.Scenes;

/// <summary>Builds the scene loader under test over scene files in a temporary content folder, a new world and a spawner that knows transforms.</summary>
internal sealed class SceneLoaderTestsBuilder : IDisposable
{
    private readonly ContentManagerTestsBuilder _files = new();
    private readonly ILogger<SceneLoader> _logger = Substitute.For<ILogger<SceneLoader>>();
    private ContentManager? _content;

    public SceneLoaderTestsBuilder()
    {
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    public World World { get; } = World.Create();

    /// <summary>The warnings the loader logged.</summary>
    public IEnumerable<object?[]> Warnings => _logger.ReceivedCalls()
        .Where(static call => call.GetMethodInfo().Name == nameof(ILogger.Log))
        .Select(static call => call.GetArguments())
        .Where(static arguments => arguments[0] is LogLevel.Warning);

    public SceneLoaderTestsBuilder WithScene(string path, string json)
    {
        _files.WithFile(path, json);
        return this;
    }

    public SceneLoader Build()
    {
        SceneSpawner spawner = new();
        spawner.Register("transform", RuntimeJsonContext.Default.Transform);
        _content = new ContentManager(_files.RootDirectory);
        return new SceneLoader(spawner, _content, World, _logger);
    }

    /// <summary>Rewrites the file and reports it changed, as the content watcher would.</summary>
    public void ChangeScene(string path, string json)
    {
        _files.WithFile(path, json);
        _content!.NotifyChanged(path);
    }

    public void Dispose()
    {
        World.Destroy(World);
        _files.Dispose();
    }
}
