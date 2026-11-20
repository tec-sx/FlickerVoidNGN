using FlickerVoid.Editor.Assets.Data;
using FlickerVoid.Engine.Runtime.Data;

namespace FlickerVoid.Editor.Assets.Tests.Data;

/// <summary>Builds a data document over Cellar.fvdata in a temporary content folder, with the editor's unsaved content.</summary>
internal sealed class DataDocumentTestsBuilder : IDisposable
{
    public const string Cellar = "Cellar.fvdata";

    private readonly UnsavedContentTestsBuilder _files = new();
    private readonly DataTypes _types = BarrelJsonContext.CreateTypes();

    public DataDocumentTestsBuilder()
    {
        _files.WithFile(Cellar, """{ "$schema": "../.schemas/data.schema.json", "type": "barrel", "litres": 80 }""");
        Content = _files.Build();
    }

    public UnsavedContent Content { get; }

    public DataDocument Build() => new(Content, _types, Cellar);

    public string ReadFile() => _files.ReadFile(Cellar);

    public void Dispose() => _files.Dispose();
}
