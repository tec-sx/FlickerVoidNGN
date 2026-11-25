using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Vehicles.Tests;

/// <summary>Builds a content folder of the files given and a game with vehicles, then checks the folder.</summary>
internal sealed class VehicleContentCheckTestsBuilder : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "FlickerVoidVehicleTests", Guid.NewGuid().ToString("N"));
    private readonly Game _game = new(NullLoggerFactory.Instance);

    public VehicleContentCheckTestsBuilder()
    {
        _game.AddVehicles();
    }

    public VehicleContentCheckTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(fullPath, json);
        return this;
    }

    public IReadOnlyList<ContentProblem> Check() => ContentCheck.Run(_game, _folder);

    public void Dispose()
    {
        _game.Dispose();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
