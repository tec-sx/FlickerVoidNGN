namespace FlickerVoid.Gameplay.World.Society.Residents;

/// <summary>Someone who lives here: wanders about where it started, resting between walks, and watches whoever it sees.</summary>
public struct Resident
{
    public Resident()
    {
    }

    /// <summary>Metres from home it wanders; 0 keeps it where it stands.</summary>
    public float WanderRadius { get; set; }

    /// <summary>Seconds it rests between walks.</summary>
    public float Rest { get; set; } = 4;
}
