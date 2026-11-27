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

    /// <summary>For a play, the frames the voice fades in over; for a stop at a frame, that frame.</summary>
    public long More { get; private init; }

    /// <summary>For a play whose start frame has passed, whether the voice skips what it missed, so it stays on the grid it was scheduled on.</summary>
    public bool CatchUp { get; private init; }

    /// <summary>Starts a voice on a bus with its gain to each ear and a low-pass cutoff in hertz, on the mixer frame <paramref name="startFrame"/>.</summary>
    public static MixerCommand Play(int voice, ISoundSource source, AudioBus bus, float left, float right, float cutoff = NoCutoff, long startFrame = 0) =>
        new() { Kind = MixerCommandKind.Play, Voice = voice, Source = source, Bus = bus, Left = left, Right = right, Cutoff = cutoff, Frames = startFrame };

    /// <summary>
    /// Starts a voice on the grid: on the mixer frame <paramref name="startFrame"/>, fading in over <paramref name="fadeInFrames"/>,
    /// and when that frame has passed already, from where it would have been by now.
    /// </summary>
    public static MixerCommand PlayOnGrid(int voice, ISoundSource source, AudioBus bus, float gain, long startFrame, long fadeInFrames = 0) =>
        new() { Kind = MixerCommandKind.Play, Voice = voice, Source = source, Bus = bus, Left = gain, Right = gain, Cutoff = NoCutoff, Frames = startFrame, More = fadeInFrames, CatchUp = true };

    public static MixerCommand SetVoice(int voice, float left, float right, float cutoff = NoCutoff) =>
        new() { Kind = MixerCommandKind.SetVoice, Voice = voice, Left = left, Right = right, Cutoff = cutoff };

    /// <summary>Fades a voice out over the frames and ends it.</summary>
    public static MixerCommand Stop(int voice, long fadeFrames) => new() { Kind = MixerCommandKind.Stop, Voice = voice, Frames = fadeFrames };

    /// <summary>Fades a voice out over the frames from the mixer frame <paramref name="atFrame"/> on and ends it; a voice that starts no sooner never sounds.</summary>
    public static MixerCommand StopAt(int voice, long atFrame, long fadeFrames) =>
        new() { Kind = MixerCommandKind.StopAt, Voice = voice, Frames = fadeFrames, More = atFrame };

    public static MixerCommand SetBus(AudioBus bus, float gain, float cutoff = NoCutoff) =>
        new() { Kind = MixerCommandKind.SetBus, Bus = bus, Left = gain, Cutoff = cutoff };

    /// <summary>For a reverb change, how big the room is (0 to 1); its wet mix is <see cref="Left"/> and its damping <see cref="Right"/>.</summary>
    public float Size { get; private init; }

    /// <summary>Sets the room the world buses (ambience, effects and dialogue) ring in: the wet mix, how big it is and how dull its walls are.</summary>
    public static MixerCommand SetReverb(float wet, float size, float damping) =>
        new() { Kind = MixerCommandKind.SetReverb, Left = wet, Right = damping, Size = size };

    /// <summary>Holds a bus's voices where they are, silent, or lets them go on.</summary>
    public static MixerCommand PauseBus(AudioBus bus, bool paused) => new() { Kind = MixerCommandKind.PauseBus, Bus = bus, Paused = paused };
}
