using FlickerVoid.Editor.Assets.Data;
using FlickerVoid.Engine.Runtime.Data;

namespace FlickerVoid.Editor.Assets.Tests.Data;

/// <summary>Builds the table view over Wines.fvtable, two wine rows, in a temporary content folder with the editor's unsaved content.</summary>
internal sealed class TableViewModelTestsBuilder : IDisposable
{
    public const string Wines = "Wines.fvtable";

    private readonly UnsavedContentTestsBuilder _files = new();
    private readonly DataTypes _types = BarrelJsonContext.CreateTypes();

    public TableViewModelTestsBuilder()
    {
        _files.WithFile(Wines, """{ "$schema": ".schemas/table.schema.json", "type": "wine", "rows": { "Red": { "year": 1998 }, "White": { "year": 2004 } } }""");
        Content = _files.Build();
    }

    public UnsavedContent Content { get; }

    public TableViewModel Build()
    {
        TableDocument document = new(Content, _types, Wines);
        return new TableViewModel(document);
    }

    /// <summary>The file as saved, after saving its unsaved edits.</summary>
    public string Save()
    {
        Content.Save(Wines);
        return _files.ReadFile(Wines);
    }

    public void Dispose() => _files.Dispose();
}
