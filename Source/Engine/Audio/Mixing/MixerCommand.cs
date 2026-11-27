namespace FlickerVoid.Engine.Audio.Mixing;

/// <summary>A request from the game thread, taken up by the audio thread before its next block.</summary>
public readonly record struct MixerCommand
{
    /// <summary>A cutoff at or above this leaves the sound unfiltered.</summary>
    public const float NoCutoff = 20000;

    public MixerCommandKind Kind { get; private init; }

    public int Voice { get; private init; }

    public ISoundSource? Source { get; private init; }

    public AudioBus Bus { get; private init; }

    public float Left { get; private init; }

    public float Right { get; private init; }

    public float Cutoff { get; private init; }

    /// <summary>For a play, the mixer frame the voice starts on (at once when it has passed); for a stop, the frames it fades over.</summary>
    public long Frames { get; private init; }

    public bool Paused { get; private init; }

    /// <summary>Starts a voice on a bus with its gain to each ear and a low-pass cutoff in hertz, on the mixer frame <paramref name="startFrame"/>.</summary>
    public static MixerCommand Play(int voice, ISoundSource source, AudioBus bus, float left, float right, float cutoff = NoCutoff, long startFrame = 0) =>
        new() { Kind = MixerCommandKind.Play, Voice = voice, Source = source, Bus = bus, Left = left, Right = right, Cutoff = cutoff, Frames = startFrame };

    public static MixerCommand SetVoice(int voice, float left, float right, float cutoff = NoCutoff) =>
        new() { Kind = MixerCommandKind.SetVoice, Voice = voice, Left = left, Right = right, Cutoff = cutoff };

    /// <summary>Fades a voice out over the frames and ends it.</summary>
    public static MixerCommand Stop(int voice, long fadeFrames) => new() { Kind = MixerCommandKind.Stop, Voice = voice, Frames = fadeFrames };

    public static MixerCommand SetBus(AudioBus bus, float gain, float cutoff = NoCutoff) =>
        new() { Kind = MixerCommandKind.SetBus, Bus = bus, Left = gain, Cutoff = cutoff };

    /// <summary>Holds a bus's voices where they are, silent, or lets them go on.</summary>
    public static MixerCommand PauseBus(AudioBus bus, bool paused) => new() { Kind = MixerCommandKind.PauseBus, Bus = bus, Paused = paused };
}
