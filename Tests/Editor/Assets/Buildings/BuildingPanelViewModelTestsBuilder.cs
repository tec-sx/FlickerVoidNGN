using FlickerVoid.Editor.Assets.Buildings;
using FlickerVoid.Engine.Buildings;

namespace FlickerVoid.Editor.Assets.Tests.Buildings;

/// <summary>Builds the creator's panel over a building document of a new building.</summary>
internal sealed class BuildingPanelViewModelTestsBuilder : IDisposable
{
    private readonly BuildingDocumentTestsBuilder _document = new();

    public BuildingPanelViewModel Build()
    {
        BuildingDocument document = _document.Build();
        return new BuildingPanelViewModel(document);
    }

    public BuildingFile Load() => _document.Load();

    public void Dispose() => _document.Dispose();
}
