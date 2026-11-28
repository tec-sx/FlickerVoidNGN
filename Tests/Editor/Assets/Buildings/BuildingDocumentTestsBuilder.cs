using System.Text.Json;

using FlickerVoid.Editor.Assets.Buildings;
using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Serialization;

namespace FlickerVoid.Editor.Assets.Tests.Buildings;

/// <summary>Builds a building document over House.fvbuilding, a new building, in a temporary content folder with the editor's unsaved content.</summary>
internal sealed class BuildingDocumentTestsBuilder : IDisposable
{
    public const string House = "House.fvbuilding";

    private readonly UnsavedContentTestsBuilder _files = new();

    public BuildingDocumentTestsBuilder()
    {
        BuildingFile house = BuildingFile.Create();
        BuildingTools.Rebuild(house);
        house.Schema = ".schemas/building.schema.json";
        string text = JsonSerializer.Serialize(house, BuildingsJsonContext.Default.BuildingFile);
        _files.WithFile(House, text);
        Content = _files.Build();
    }

    public UnsavedContent Content { get; }

    /// <summary>The folder the content lives in, as the content's full paths start.</summary>
    public string Folder => _files.Folder;

    public BuildingDocument Build() => new(Content, House);

    /// <summary>The building as every load of the file sees it, such as a placed copy's.</summary>
    public BuildingFile Load() => Content.Load(House, BuildingsJsonContext.Default.BuildingFile);

    /// <summary>The building the file on disk holds.</summary>
    public BuildingFile ReadDisk()
    {
        string text = _files.ReadFile(House);
        return JsonSerializer.Deserialize(text, BuildingsJsonContext.Default.BuildingFile)!;
    }

    public void Dispose() => _files.Dispose();
}
