using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Runtime.Tests.Content;

/// <summary>
/// Builds a content folder of the files given and a game that registers a <c>signpost</c> component, a <c>journey</c> data asset with
/// <c>walk</c> legs, a <c>fare</c> table row and the <c>.fvtrail</c> file kind, then checks the folder.
/// </summary>
internal sealed class ContentCheckTestsBuilder : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "FlickerVoidContentCheckTests", Guid.NewGuid().ToString("N"));
    private readonly Game _game = new(NullLoggerFactory.Instance);

    public ContentCheckTestsBuilder()
    {
        _game.Spawner.Register("signpost", ContentCheckTestsJsonContext.Default.Signpost);
        _game.Data.AddResolver(ContentCheckTestsJsonContext.Default);
        _game.Data.Register<Journey>("journey");
        _game.Data.RegisterDerived<Leg, WalkLeg>("walk");
        _game.Data.RegisterRow<FareRow>("fare");
        _game.Schemas.AddFile("trail", ".fvtrail", static () => ContentCheckTestsJsonContext.Default.Trail);
    }

    public ContentCheckTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(fullPath, json);
        return this;
    }

    public IReadOnlyList<ContentProblem> Check()
    {
        return ContentCheck.Run(_game, _folder);
    }

    public void Dispose()
    {
        _game.Dispose();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
