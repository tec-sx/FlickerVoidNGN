using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Cameras.Triggers;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Tests.Music;

public sealed class MusicAreaLabelsTests
{
    private readonly World _world = World.Create();

    [Fact]
    public void LabelsEachMusicAreaWithItsNameAndTheTracksItTurnsOnAtTheCentreOfItsBox()
    {
        Transform transform = new() { Position = new Vector3(2, 0, 0) };
        TriggerArea area = new() { Size = new Vector3(4, 2, 4), Offset = new Vector3(0, 1, 0) };
        MusicArea music = new() { Layers = ["Drums", "Hats"] };
        _world.Create(transform, area, music, new EntityName("Inn Room Music"));
        _world.Create(transform, area);
        EditorCamera camera = new();
        camera.LookAt(Vector3.Zero, 0.3f, -0.4f, 10);

        IReadOnlyList<MusicAreaLabel> labels = MusicAreaLabels.Collect(_world, camera, 640, 480);

        MusicAreaLabel label = labels.Should().ContainSingle().Which;
        label.Name.Should().Be("Inn Room Music");
        label.Layers.Should().Be("Drums, Hats");
        label.X.Should().BeInRange(0, 640);
        label.Y.Should().BeInRange(0, 480);
    }
}
