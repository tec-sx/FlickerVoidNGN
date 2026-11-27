namespace FlickerVoid.Engine.Audio.Sounds;

/// <summary>Marks the entity whose ears hear the world, such as the protagonist; they turn with the camera, so the left of the screen sounds from the left.</summary>
public struct AudioListener
{
    public AudioListener()
    {
    }

    /// <summary>How high above the entity's place the ears are, in metres.</summary>
    public float Height { get; set; } = 1.5f;
}
