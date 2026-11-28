using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Engine.Audio.Music;

/// <summary>
/// What gameplay knows of music and ambience: it plays <c>music</c> data assets by their content path, one stack of cues for
/// each bus, and asks for their tracks by name, never touching the mixer, so another backend can stand behind it.
/// </summary>
public interface IMusicPlayer
{
    /// <summary>Plays a cue as its bus's lowest, taking the place of the one there on its next bar; playing the cue that is there changes nothing.</summary>
    void Play(string cue);

    /// <summary>Stops a bus's cues on the next bar of the one heard.</summary>
    void Stop(AudioBus bus = AudioBus.Music);

    /// <summary>
    /// Puts a cue over the one heard on its bus: from that one's next bar the new cue's fill-in plays over it, then it is held
    /// where it is and the new cue starts.
    /// </summary>
    void Push(string cue);

    /// <summary>
    /// Takes the cue heard off the one below it: from its next bar its fill-out plays, then the cue below continues from where it
    /// was held, with the tracks wanted now.
    /// </summary>
    void Pop(AudioBus bus = AudioBus.Music);

    /// <summary>Asks for the tracks of this name, as a music area does while someone stands in it; asked twice, it takes two releases.</summary>
    void RequestLayer(string layer);

    void ReleaseLayer(string layer);

    /// <summary>Plays a sound file once over the music, from the next line of the grid of the cue heard.</summary>
    void PlayStinger(string file, MusicQuantize quantize = MusicQuantize.Beat, float gain = 1);

    /// <summary>The content path of the cue heard on a bus; null when there is none.</summary>
    string? GetCurrent(AudioBus bus = AudioBus.Music);

    /// <summary>Where the cue heard on a bus is on its grid; bar 1, beat 1 without a cue or a tempo.</summary>
    MusicPosition GetPosition(AudioBus bus = AudioBus.Music);

    /// <summary>Whether a track of the cue heard on a bus sounds or is coming in.</summary>
    bool IsLayerOn(string layer, AudioBus bus = AudioBus.Music);

    /// <summary>
    /// Jumps the cue heard on a bus to a bar (counted from 1, with fractions): its tracks stop and start again there at once, without
    /// their fill-ins, so a place in the song can be heard without waiting for it.
    /// </summary>
    void Seek(double bar, AudioBus bus = AudioBus.Music);

    /// <summary>Stops a bus's cues now, fading over a few milliseconds, instead of on the next bar.</summary>
    void StopNow(AudioBus bus = AudioBus.Music);

    /// <summary>Silences the tracks of this name without taking them out of the cue, as a mixer's mute does.</summary>
    void SetTrackMuted(string layer, bool muted);

    /// <summary>Hears only the tracks soloed, while any is.</summary>
    void SetTrackSoloed(string layer, bool soloed);

    /// <summary>Scales the tracks of this name, from 0 up, on top of their cue's own gain.</summary>
    void SetTrackGain(string layer, float gain);

    /// <summary>The loudest sample, 0 to 1, a track of the cue heard on a bus put out since this was last asked.</summary>
    float TakeLayerLevel(string layer, AudioBus bus = AudioBus.Music);
}
