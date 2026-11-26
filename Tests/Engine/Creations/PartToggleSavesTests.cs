using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Creations.Tests;

public sealed class PartToggleSavesTests : IDisposable
{
    private const float Precision = 0.0001f;

    private readonly JointSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void KeepsWhichDoorsAreOpen()
    {
        PartJoint hinge = JointSystemTestsBuilder.Hinge(new Vector3(1, 0, 0), 0, 90, 90);
        _builder.PlaceDoor(hinge, open: true);
        PartToggleSaves saves = new(_builder.World, _builder.Parts, _builder.System);

        SavedPartToggles? saved = saves.Save(_builder.Root);

        saved.Should().NotBeNull();
        saved!.Open.Should().Equal(new Dictionary<string, bool> { ["door"] = true });
    }

    [Fact]
    public void KeepsNothingForACreationWithoutToggles()
    {
        PartJoint hinge = JointSystemTestsBuilder.Hinge(new Vector3(1, 0, 0), 0, 90, 90);
        _builder.Place(new Vector3(2, 0, 0), hinge);
        PartToggleSaves saves = new(_builder.World, _builder.Parts, _builder.System);

        SavedPartToggles? saved = saves.Save(_builder.Root);

        saved.Should().BeNull();
    }

    [Fact]
    public void RestoringOpensAClosedDoorAtOnce()
    {
        PartJoint hinge = JointSystemTestsBuilder.Hinge(new Vector3(1, 0, 0), 0, 90, 90);
        _builder.PlaceDoor(hinge, open: false);
        PartToggleSaves saves = new(_builder.World, _builder.Parts, _builder.System);

        saves.Restore(_builder.Root, new SavedPartToggles { Open = new Dictionary<string, bool> { ["door"] = true } });

        _builder.World.Get<Toggle>(_builder.Part).Open.Should().BeTrue();
        _builder.PositionOfJoint().Should().BeApproximately(90, Precision);
        Transform transform = _builder.TransformOfPart();
        Vector3 expected = new Vector3(1, 0, 0) + Vector3.Transform(Vector3.UnitX, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2));
        Vector3.Distance(transform.Position, expected).Should().BeLessThan(Precision);
    }

    [Fact]
    public void RestoringShutsAnOpenDoorAtOnce()
    {
        PartJoint hinge = JointSystemTestsBuilder.Hinge(new Vector3(1, 0, 0), 0, 90, 90);
        _builder.PlaceDoor(hinge, open: true);
        PartToggleSaves saves = new(_builder.World, _builder.Parts, _builder.System);

        saves.Restore(_builder.Root, new SavedPartToggles { Open = new Dictionary<string, bool> { ["door"] = false } });

        _builder.World.Get<Toggle>(_builder.Part).Open.Should().BeFalse();
        _builder.PositionOfJoint().Should().BeApproximately(0, Precision);
    }

    [Fact]
    public void RestoringSkipsPartsTheCreationNoLongerHas()
    {
        PartJoint hinge = JointSystemTestsBuilder.Hinge(new Vector3(1, 0, 0), 0, 90, 90);
        _builder.PlaceDoor(hinge, open: false);
        PartToggleSaves saves = new(_builder.World, _builder.Parts, _builder.System);

        Action restore = () => saves.Restore(_builder.Root, new SavedPartToggles { Open = new Dictionary<string, bool> { ["door.9"] = true } });

        restore.Should().NotThrow();
        _builder.PositionOfJoint().Should().Be(0);
    }
}
