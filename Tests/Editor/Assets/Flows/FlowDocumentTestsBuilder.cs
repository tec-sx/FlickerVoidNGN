using FlickerVoid.Editor.Assets.Flows;
using FlickerVoid.Engine.Flow;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Assets.Tests.Flows;

/// <summary>Builds a flow document over Cellar.fvflow in a temporary content folder, with the node types of a game with flow.</summary>
internal sealed class FlowDocumentTestsBuilder : IDisposable
{
    public const string Cellar = "Cellar.fvflow";

    private readonly UnsavedContentTestsBuilder _files = new();
    private readonly Game _game = new(NullLoggerFactory.Instance);

    public FlowDocumentTestsBuilder()
    {
        _game.AddFlow();
        _files.WithFile(Cellar, """
            {
              "$schema": "../.schemas/flow.schema.json",
              "nodes": [ { "$type": "start", "id": "start" }, { "$type": "finish", "id": "finish", "x": 200 } ],
              "links": [ { "from": "start", "to": "finish" } ]
            }
            """);
        Content = _files.Build();
    }

    public UnsavedContent Content { get; }

    public FlowDocument Build() => new(Content, _game.Data, Cellar);

    public string ReadFile() => _files.ReadFile(Cellar);

    public void Dispose()
    {
        _files.Dispose();
        _game.Dispose();
    }
}
