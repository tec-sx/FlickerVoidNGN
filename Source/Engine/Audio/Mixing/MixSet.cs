namespace FlickerVoid.Engine.Audio.Mixing;

/// <summary>The mix states of a game (a <c>mix</c> data asset, <c>Audio/Mix.fvmix</c>); a game without one has <see cref="Default"/>.</summary>
public sealed class MixSet
{
    /// <summary>The extension of mix files.</summary>
    public const string Extension = ".fvmix";

    /// <summary>Where the audio system looks for the game's mix, in content.</summary>
    public const string Path = "Audio/Mix.fvmix";

    public const string Indoors = "Indoors";

    public const string Paused = "Paused";

    public const string Talking = "Talking";

    public const string HubOpen = "Hub open";

    public List<MixState> States { get; set; } = [];

    /// <summary>Indoors dulls the ambience, talking lowers the music, the hub lowers the world, and a pause lowers the music.</summary>
    public static MixSet Default()
    {
        return new MixSet
        {
            States =
            [
                new MixState { Name = Indoors, Fade = 0.3f, Ducks = [new MixDuck { Bus = AudioBus.Ambience, Gain = 0.6f, Cutoff = 1200 }] },
                new MixState { Name = Talking, Fade = 0.4f, Ducks = [new MixDuck { Bus = AudioBus.Music, Gain = 0.5f }, new MixDuck { Bus = AudioBus.Ambience, Gain = 0.6f }] },
                new MixState { Name = HubOpen, Fade = 0.5f, Ducks = [new MixDuck { Bus = AudioBus.Ambience, Gain = 0.4f, Cutoff = 3000 }, new MixDuck { Bus = AudioBus.Effects, Gain = 0.6f }] },
                new MixState { Name = Paused, Fade = 0.2f, Ducks = [new MixDuck { Bus = AudioBus.Music, Gain = 0.7f }] },
            ],
        };
    }
}
