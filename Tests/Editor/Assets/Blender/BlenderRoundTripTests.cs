using FlickerVoid.Editor.Assets.Blender;

namespace FlickerVoid.Editor.Assets.Tests.Blender;

public sealed class BlenderRoundTripTests : IDisposable
{
    private const string Van = BlenderRoundTripTestsBuilder.Van;

    private readonly BlenderRoundTripTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TheFirstOpenExportsTheGreyboxesAndStartsBlenderOnThemWithTheScript()
    {
        BlenderRoundTrip trip = _builder.Build();
        IReadOnlyList<string>? arguments = null;
        _builder.Launcher.Launch("/opt/blender/blender", Arg.Do<IReadOnlyList<string>>(given => arguments = given)).Returns(true);

        string said = trip.Open(Van, _builder.Vehicle);

        string look = _builder.FullPath("Vans/Van.glb");
        string blend = _builder.FullPath("Vans/Van.blend");
        File.Exists(look).Should().BeTrue();
        arguments.Should().NotBeNull();
        arguments![0].Should().Be("--python");
        File.ReadAllText(arguments[1]).Should().Contain("export_scene.gltf");
        arguments.Skip(2).Should().Equal("--", look, blend);
        said.Should().Contain("Blender");
    }

    [Fact]
    public void ALaterOpenStartsBlenderOnItsOwnFileAndLeavesTheLookAlone()
    {
        BlenderRoundTrip trip = _builder.WithFile("Vans/Van.blend").WithFile("Vans/Van.glb").Build();
        IReadOnlyList<string>? arguments = null;
        _builder.Launcher.Launch(Arg.Any<string>(), Arg.Do<IReadOnlyList<string>>(given => arguments = given)).Returns(true);

        trip.Open(Van, _builder.Vehicle);

        string blend = _builder.FullPath("Vans/Van.blend");
        arguments![0].Should().Be(blend);
        arguments[1].Should().Be("--python");
        File.ReadAllText(_builder.FullPath("Vans/Van.glb")).Should().Be("made in Blender");
    }

    [Fact]
    public void WithoutBlenderItSaysHowToNameItAndStartsNothing()
    {
        BlenderRoundTrip trip = _builder.WithoutBlender().Build();

        string said = trip.Open(Van, _builder.Vehicle);

        said.Should().Contain(BlenderLocator.Variable);
        _builder.Launcher.DidNotReceiveWithAnyArgs().Launch(default!, default!);
        File.Exists(_builder.FullPath("Vans/Van.glb")).Should().BeFalse();
    }

    [Fact]
    public void BlenderIsFoundByTheVariableThenOnThePathThenWhereItInstallsTheNewestFirst()
    {
        string root = Path.Combine(Path.GetTempPath(), "FlickerVoidBlenderTests", Guid.NewGuid().ToString("N"));
        string old = Path.Combine(root, "Blender 4.2");
        string latest = Path.Combine(root, "Blender 5.3");
        Directory.CreateDirectory(old);
        Directory.CreateDirectory(latest);
        File.WriteAllText(Path.Combine(old, BlenderLocator.Program), string.Empty);
        File.WriteAllText(Path.Combine(latest, BlenderLocator.Program), string.Empty);
        string named = Path.Combine(old, BlenderLocator.Program);

        string? byVariable = BlenderLocator.Find(named, [], [root]);
        string? onPath = BlenderLocator.Find(null, [old], [root]);
        string? installed = BlenderLocator.Find(null, [], [root]);
        string? nowhere = BlenderLocator.Find(null, [], []);

        byVariable.Should().Be(named);
        onPath.Should().Be(named);
        installed.Should().Be(Path.Combine(latest, BlenderLocator.Program));
        nowhere.Should().BeNull();
        Directory.Delete(root, recursive: true);
    }
}
