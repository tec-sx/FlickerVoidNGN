namespace FlickerVoid.Engine.Rendering.Shadows;

/// <summary>
/// Hands out squares of the one depth texture every shadow is drawn into: the top-left quarter for the sun, and equal tiles of
/// the rest for spot lights (one each) and point lights (one per cube face), first come first served each frame.
/// </summary>
public sealed class ShadowAtlas
{
    public const int Size = 4096;
    public const int SunTileSize = 2048;
    public const int TileSize = 512;

    private const int TilesPerRow = Size / TileSize;
    private const int SunTilesPerRow = SunTileSize / TileSize;

    private int _next;
    private int _handedOut;

    public ShadowTile Sun => new(0, 0, SunTileSize);

    /// <summary>The tiles still free this frame.</summary>
    public int FreeTiles => (TilesPerRow * TilesPerRow) - (SunTilesPerRow * SunTilesPerRow) - _handedOut;

    /// <summary>Frees every tile for the next frame.</summary>
    public void Reset()
    {
        _next = 0;
        _handedOut = 0;
    }

    public bool TryAllocate(out ShadowTile tile)
    {
        while (_next < TilesPerRow * TilesPerRow)
        {
            int column = _next % TilesPerRow;
            int row = _next / TilesPerRow;
            _next++;
            bool insideSun = column < SunTilesPerRow && row < SunTilesPerRow;
            if (!insideSun)
            {
                tile = new ShadowTile(column * TileSize, row * TileSize, TileSize);
                _handedOut++;
                return true;
            }
        }

        tile = default;
        return false;
    }
}

