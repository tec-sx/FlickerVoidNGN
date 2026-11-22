using System.Numerics;

using FlickerVoid.Engine.Maps.Definitions;

namespace FlickerVoid.Engine.Maps.Tests.Definitions;

public sealed class MapDefinitionTests
{
    private readonly MapDefinitionTestsBuilder _builder = new();

    [Fact]
    public void LayersMapTheGroundToTheirImageWithNorthUp()
    {
        MapLayer town = _builder.Town;

        Vector2 northWest = town.WorldToUv(new Vector3(-50, 0, -50));
        Vector2 east = town.WorldToUv(new Vector3(25, 0, 0));
        Vector3 back = town.UvToWorld(new Vector2(0.75f, 0.5f), 2);

        northWest.Should().Be(Vector2.Zero);
        east.Should().Be(new Vector2(0.75f, 0.5f));
        back.Should().Be(new Vector3(25, 2, 0));
        town.RadiusToExtentUv(10).Should().Be(new Vector2(0.1f, 0.1f));
    }

    [Fact]
    public void AHeightLimitedLayerWinsWhereItCoversAPlace()
    {
        MapDefinition map = _builder.Build();

        int inCellar = map.FindLayerAt(new Vector3(5, -3, 10));
        int aboveCellar = map.FindLayerAt(new Vector3(5, 0, 10));
        int outside = map.FindLayerAt(new Vector3(80, 0, 0));
        int baseOfCellar = map.FindBaseLayerAt(new Vector3(5, -3, 10));

        inCellar.Should().Be(1);
        aboveCellar.Should().Be(0);
        outside.Should().Be(-1);
        baseOfCellar.Should().Be(0);
        map.FindLayerByName("Cellar").Should().Be(1);
    }
}
