namespace FlickerVoid.Engine.Ai.Senses;

/// <summary>What an entity sees and hears of the <see cref="Perceivable"/> entities around it; it looks down its +Z axis.</summary>
public struct Perception
{
    public Perception()
    {
    }

    /// <summary>Metres within which it notices what it sees.</summary>
    public float SightRadius { get; set; } = 8;

    /// <summary>Metres beyond which it loses sight of what it saw.</summary>
    public float LoseSightRadius { get; set; } = 10;

    /// <summary>Degrees either side of straight ahead that it sees.</summary>
    public float SightAngle { get; set; } = 60;

    /// <summary>Metres above its feet that it looks from.</summary>
    public float EyeHeight { get; set; } = 1.5f;

    /// <summary>Metres within which it hears a noise of loudness 1; louder and quieter noises carry further and less far.</summary>
    public float HearingRange { get; set; } = 10;

    /// <summary>Seconds it remembers a noise it heard.</summary>
    public float Memory { get; set; } = 5;
}
