namespace FlickerVoid.Engine.Ai.Paths;

/// <summary>The size of the characters paths are built for, and how finely the ground is sampled; distances in metres, slopes in degrees.</summary>
public sealed class NavigationSettings
{
    /// <summary>How far paths keep from walls and obstacles.</summary>
    public float AgentRadius { get; set; } = 0.3f;

    /// <summary>The headroom a character needs.</summary>
    public float AgentHeight { get; set; } = 1.6f;

    /// <summary>The highest step a character climbs, as the movement system's step height.</summary>
    public float MaxClimb { get; set; } = 0.4f;

    public float MaxSlope { get; set; } = 45;

    /// <summary>The width of a sample of the ground; smaller follows edges more closely and builds slower.</summary>
    public float CellSize { get; set; } = 0.1f;

    public float CellHeight { get; set; } = 0.05f;

    /// <summary>The width of a tile of the mesh in cells; an obstacle that moves builds only the tiles it touches again.</summary>
    public int TileSize { get; set; } = 64;

    /// <summary>How far from a point, across and up or down, a path looks for ground to start or end on.</summary>
    public float SearchDistance { get; set; } = 1;

    public float SearchHeight { get; set; } = 2;
}
