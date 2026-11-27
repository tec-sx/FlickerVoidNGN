namespace FlickerVoid.Engine.Audio.Mixing;

/// <summary>What a <see cref="MixerCommand"/> asks of the mixer.</summary>
public enum MixerCommandKind
{
    Play,
    SetVoice,
    Stop,
    StopAt,
    SetBus,
    PauseBus,
    SetReverb,
}
