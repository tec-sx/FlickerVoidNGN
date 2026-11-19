namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>Builds an empty shadow atlas.</summary>
internal sealed class ShadowAtlasTestsBuilder
{
    public ShadowAtlas Build() => new();

    public static List<ShadowTile> AllocateAll(ShadowAtlas atlas)
    {
        List<ShadowTile> tiles = [];
        while (atlas.TryAllocate(out ShadowTile tile))
        {
            tiles.Add(tile);
        }

        return tiles;
    }

    public static bool Overlaps(ShadowTile a, ShadowTile b)
    {
        bool apartX = a.X + a.Size <= b.X || b.X + b.Size <= a.X;
        bool apartY = a.Y + a.Size <= b.Y || b.Y + b.Size <= a.Y;
        return !apartX && !apartY;
    }
}
