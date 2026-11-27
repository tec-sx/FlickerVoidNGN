namespace FlickerVoid.Engine.Audio.Sounds;

/// <summary>Gives the indoor volume on the same entity a reverberation the world's sounds ring in while the listener stands inside it.</summary>
public struct RoomReverb
{
    public RoomReverb()
    {
    }

    /// <summary>How much of the reverberation is heard, from 0 to 1.</summary>
    public float Wet { get; set; } = 0.3f;

    /// <summary>How big the room is, from 0 (a closet) to 1 (a hall).</summary>
    public float Size { get; set; } = 0.4f;

    /// <summary>How much its walls soak up high sounds, from 0 (stone) to 1 (curtains).</summary>
    public float Damping { get; set; } = 0.4f;
}
