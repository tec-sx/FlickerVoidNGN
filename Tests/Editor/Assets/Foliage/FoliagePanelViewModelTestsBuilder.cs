using FlickerVoid.Editor.Assets.Foliage;
using FlickerVoid.Engine.Foliage;

namespace FlickerVoid.Editor.Assets.Tests.Foliage;

/// <summary>Builds the creator's panel over a foliage document of a new tree, drawing its new seeds from a fixed chance.</summary>
internal sealed class FoliagePanelViewModelTestsBuilder : IDisposable
{
    private readonly FoliageDocumentTestsBuilder _document = new();

    public FoliagePanelViewModel Build()
    {
        FoliageDocument document = _document.Build();
        Random random = new(5);
        return new FoliagePanelViewModel(document, random);
    }

    public FoliageFile Load() => _document.Load();

    public void Dispose() => _document.Dispose();
}
