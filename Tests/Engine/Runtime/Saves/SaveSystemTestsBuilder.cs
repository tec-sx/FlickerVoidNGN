using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Runtime.Tests.Saves;

/// <summary>Builds a game with saves in a temporary folder.</summary>
internal sealed class SaveSystemTestsBuilder : IDisposable
{
    private readonly string _folder;

    public SaveSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public SaveSystem Saves { get; private set; } = null!;

    public string Folder => _folder;

    public SaveSystemTestsBuilder Build()
    {
        SaveSettings settings = new() { Folder = _folder };
        Saves = Game.AddSaves(settings);
        return this;
    }

    /// <summary>Lets the game play for <paramref name="seconds"/>.</summary>
    public void Play(double seconds)
    {
        TimeSpan step = TimeSpan.FromSeconds(0.25);
        for (double passed = 0; passed < seconds; passed += step.TotalSeconds)
        {
            Game.Tick(step);
        }
    }

    public void Dispose()
    {
        Game.Dispose();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
