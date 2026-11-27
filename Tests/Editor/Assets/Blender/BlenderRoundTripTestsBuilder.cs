using FlickerVoid.Editor.Assets.Blender;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Assets.Tests.Blender;

/// <summary>Builds the round trip over a temporary content folder, with a launcher that only notes what it was asked to start.</summary>
internal sealed class BlenderRoundTripTestsBuilder : IDisposable
{
    public const string Van = "Vans/Van.fvvehicle";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "FlickerVoidBlenderTests", Guid.NewGuid().ToString("N"));
    private string? _blender = "/opt/blender/blender";

    public BlenderRoundTripTestsBuilder()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "Vans"));
        Content = new ContentManager(_folder);
    }

    public ContentManager Content { get; }

    public IBlenderLauncher Launcher { get; } = Substitute.For<IBlenderLauncher>();

    public VehicleFile Vehicle { get; } = VehicleFile.Create(VehicleType.Car);

    public string FullPath(string path) => Content.GetFullPath(path);

    public BlenderRoundTripTestsBuilder WithoutBlender()
    {
        _blender = null;
        return this;
    }

    public BlenderRoundTripTestsBuilder WithFile(string path)
    {
        File.WriteAllText(FullPath(path), "made in Blender");
        return this;
    }

    public BlenderRoundTrip Build()
    {
        Launcher.Launch(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns(true);
        return new BlenderRoundTrip(Content, Launcher, () => _blender);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
