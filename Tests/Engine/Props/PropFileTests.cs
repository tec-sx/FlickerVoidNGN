using System.Numerics;

namespace FlickerVoid.Engine.Props.Tests;

public sealed class PropFileTests
{
    [Fact]
    public void ANewPropIsOneMetreBox()
    {
        PropFile prop = PropFile.Create();

        prop.GetParts().Select(part => part.Id).Should().Equal("part.1");
        prop.Parts[0].Part.Mesh.Points.Max(point => point.Y).Should().Be(1, "it stands on its origin");
    }

    [Fact]
    public void BoxesTakeTheFirstFreeNumberAndDoorsComeAfterTheParts()
    {
        PropFile prop = PropFileTestsBuilder.WithDoor();
        prop.Remove("part.1").Should().BeTrue();

        string added = prop.AddBox(new Vector3(2, 1, 2), new Vector3(0, 1, 0));

        added.Should().Be("part.1");
        prop.GetParts().Select(part => part.Id).Should().Equal("part.2", "part.1", "door.1");
    }

    [Fact]
    public void RemovingAPartTakesTheDoorsOnItButAnUnknownNameIsRefused()
    {
        PropFile prop = PropFileTestsBuilder.WithDoor();

        prop.Remove("part.9").Should().BeFalse();
        prop.Remove("part.2").Should().BeTrue();

        prop.Doors.Should().BeEmpty();
    }
}
