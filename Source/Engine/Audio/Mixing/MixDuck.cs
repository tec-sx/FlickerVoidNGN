using System.ComponentModel.DataAnnotations;

namespace FlickerVoid.Engine.Audio.Mixing;

/// <summary>What a <see cref="MixState"/> does to one bus: scales its volume and, below the cutoff, dulls it.</summary>
public sealed class MixDuck
{
    public AudioBus Bus { get; set; } = AudioBus.Music;

    [Range(0, 1)]
    public float Gain { get; set; } = 1;

    /// <summary>The low-pass cutoff in hertz; 20000 leaves the bus unfiltered.</summary>
    [Range(100, 20000)]
    public float Cutoff { get; set; } = MixerCommand.NoCutoff;
}
