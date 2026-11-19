namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class ShadowAtlasTests
{
    private readonly ShadowAtlasTestsBuilder _builder = new();

    [Fact]
    public void KeepsTheTopLeftQuarterForTheSun()
    {
        ShadowAtlas atlas = _builder.Build();

        atlas.Sun.Should().Be(new ShadowTile(0, 0, ShadowAtlas.SunTileSize));
    }

    [Fact]
    public void HandsOutEveryOtherTileOnce()
    {
        ShadowAtlas atlas = _builder.Build();

        List<ShadowTile> tiles = ShadowAtlasTestsBuilder.AllocateAll(atlas);

        int expected = (ShadowAtlas.Size * ShadowAtlas.Size - ShadowAtlas.SunTileSize * ShadowAtlas.SunTileSize) / (ShadowAtlas.TileSize * ShadowAtlas.TileSize);
        tiles.Should().HaveCount(expected).And.OnlyHaveUniqueItems();
        atlas.FreeTiles.Should().Be(0);
        tiles.Should().NotContain(tile => ShadowAtlasTestsBuilder.Overlaps(tile, atlas.Sun));
        tiles.Should().OnlyContain(tile => tile.X + tile.Size <= ShadowAtlas.Size && tile.Y + tile.Size <= ShadowAtlas.Size);
    }

    [Fact]
    public void HandsTilesOutAgainAfterAReset()
    {
        ShadowAtlas atlas = _builder.Build();
        ShadowAtlasTestsBuilder.AllocateAll(atlas);

        atlas.Reset();

        atlas.FreeTiles.Should().BeGreaterThan(0);
        atlas.TryAllocate(out _).Should().BeTrue();
    }
}
