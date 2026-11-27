using System.Text.Json;

using FlickerVoid.Editor.Assets.Foliage;
using FlickerVoid.Engine.Foliage;
using FlickerVoid.Engine.Foliage.Serialization;

namespace FlickerVoid.Editor.Assets.Tests.Foliage;

/// <summary>Builds a foliage document over Oak.fvfoliage, a new tree, in a temporary content folder with the editor's unsaved content.</summary>
internal sealed class FoliageDocumentTestsBuilder : IDisposable
{
    public const string Oak = "Oak.fvfoliage";

    private readonly UnsavedContentTestsBuilder _files = new();

    public FoliageDocumentTestsBuilder()
    {
        FoliageFile oak = FoliageFile.Create(FoliageKind.Tree);
        oak.Schema = ".schemas/foliage.schema.json";
        string text = JsonSerializer.Serialize(oak, FoliageJsonContext.Default.FoliageFile);
        _files.WithFile(Oak, text);
        Content = _files.Build();
    }

    public UnsavedContent Content { get; }

    public FoliageDocument Build() => new(Content, Oak);

    /// <summary>The plant as every load of the file sees it, such as a placed copy's.</summary>
    public FoliageFile Load() => Content.Load(Oak, FoliageJsonContext.Default.FoliageFile);

    /// <summary>The plant the file on disk holds.</summary>
    public FoliageFile ReadDisk()
    {
        string text = _files.ReadFile(Oak);
        return JsonSerializer.Deserialize(text, FoliageJsonContext.Default.FoliageFile)!;
    }

    public void Dispose() => _files.Dispose();
}
