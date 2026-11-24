using Arch.Core;

namespace FlickerVoid.Engine.Ai.Senses;

/// <summary>What a perceiver sees and the noises it remembers while playing.</summary>
public struct Perceived
{
    public List<Entity> Seen { get; set; }

    public List<HeardNoise> Heard { get; set; }

    /// <summary>Seconds until it looks again.</summary>
    public float UntilLook { get; set; }
}
