namespace FlickerVoid.Engine.Characters.Movement;

/// <summary>A character that walks where it is sent, point and click: how fast it walks and turns, and how close counts as there.</summary>
public struct CharacterMover
{
    public CharacterMover()
    {
    }

    /// <summary>Metres per second.</summary>
    public float Speed { get; set; } = 1.8f;

    /// <summary>Degrees per second the character turns to face where it walks.</summary>
    public float TurnSpeed { get; set; } = 540;

    /// <summary>Metres from the destination at which the character stops.</summary>
    public float ArriveDistance { get; set; } = 0.05f;
}
