using FlickerVoid.Editor.Assets.Vehicles;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Assets.Tests.Vehicles;

/// <summary>Builds the creator's panel over a vehicle document of a new car.</summary>
internal sealed class VehiclePanelViewModelTestsBuilder : IDisposable
{
    private readonly VehicleDocumentTestsBuilder _document = new();

    public UnsavedContent Content => _document.Content;

    public VehiclePanelViewModel Build()
    {
        VehicleDocument document = _document.Build();
        return new VehiclePanelViewModel(document);
    }

    public VehicleFile Load() => _document.Load();

    public void Dispose() => _document.Dispose();
}
