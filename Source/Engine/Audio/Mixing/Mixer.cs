using System.Collections.Concurrent;

namespace FlickerVoid.Engine.Audio.Mixing;

/// <summary>
/// Mixes voices into buses and the buses into one stereo output, counting the frames it has rendered, the clock everything is
/// scheduled on. The game thread sends commands and takes the voices that ended; a device's audio thread calls
/// <see cref="Render"/>, which neither allocates nor locks.
/// </summary>
public sealed class Mixer
{
    public const int MaxVoices = 64;

    private const float CutoffFollow = 0.1f;
    private const float WetFollow = 0.02f;
    private const float CutoffSnap = 50;

    private static readonly int BusCount = Enum.GetValues<AudioBus>().Length;

    private readonly ConcurrentQueue<MixerCommand> _commands = new();
    private readonly ConcurrentQueue<int> _finished = new();
    private readonly Voice[] _voices = new Voice[MaxVoices];
    private readonly Bus[] _buses = new Bus[BusCount];
    private readonly float[] _scratch = new float[AudioFormat.BlockFrames * AudioFormat.Channels];
    private readonly float[] _send = new float[AudioFormat.BlockFrames * AudioFormat.Channels];
    private readonly Reverb _reverb = new();
    private float _wet;
    private float _targetWet;
    private long _position;

    public Mixer()
    {
        for (int index = 0; index < _voices.Length; index++)
        {
            _voices[index] = new Voice();
        }

        for (int index = 0; index < _buses.Length; index++)
        {
            _buses[index] = new Bus();
        }
    }

    /// <summary>The frames rendered so far.</summary>
    public long Position => Interlocked.Read(ref _position);

    public void Send(in MixerCommand command) => _commands.Enqueue(command);

    /// <summary>Takes the id of a voice that ended (its source ran out, it was stopped, or no voice was free for it) since the last call.</summary>
    public bool TryTakeFinished(out int voice) => _finished.TryDequeue(out voice);

    /// <summary>Renders the next frames of interleaved stereo into <paramref name="output"/>.</summary>
    public void Render(Span<float> output)
    {
        int blockSamples = AudioFormat.BlockFrames * AudioFormat.Channels;
        while (output.Length > 0)
        {
            int samples = Math.Min(output.Length, blockSamples);
            RenderBlock(output[..samples]);
            output = output[samples..];
        }
    }

    private static float Follow(float cutoff, float target)
    {
        float difference = target - cutoff;
        if (MathF.Abs(difference) <= CutoffSnap)
        {
            return target;
        }

        return cutoff + (difference * CutoffFollow);
    }

    private void RenderBlock(Span<float> output)
    {
        TakeCommands();
        int frames = output.Length / AudioFormat.Channels;
        foreach (Bus bus in _buses)
        {
            Array.Clear(bus.Buffer, 0, output.Length);
        }

        foreach (Voice voice in _voices)
        {
            if (voice.Active)
            {
                MixVoice(voice, frames);
            }
        }

        Bus master = _buses[(int)AudioBus.Master];
        Span<float> mix = master.Buffer.AsSpan(0, output.Length);
        Span<float> send = _send.AsSpan(0, output.Length);
        send.Clear();
        for (int index = 1; index < _buses.Length; index++)
        {
            Bus bus = _buses[index];
            Span<float> buffer = bus.Buffer.AsSpan(0, output.Length);
            bool rings = (AudioBus)index is AudioBus.Ambience or AudioBus.Effects or AudioBus.Dialogue;
            MixBus(bus, buffer, mix, rings ? send : default);
        }

        _wet += (_targetWet - _wet) * WetFollow;
        if (_wet > 0.0001f || _targetWet > 0)
        {
            _reverb.Process(send, mix, _wet);
        }

        master.Buffer.AsSpan(0, output.Length).CopyTo(_scratch);
        Span<float> whole = _scratch.AsSpan(0, output.Length);
        output.Clear();
        MixBus(master, whole, output);
        for (int index = 0; index < output.Length; index++)
        {
            output[index] = Math.Clamp(output[index], -1, 1);
        }

        Interlocked.Add(ref _position, frames);
    }

    /// <summary>Filters a bus's block, and adds it to <paramref name="into"/> at its gain, ramped across the block.</summary>
    private static void MixBus(Bus bus, Span<float> buffer, Span<float> into, Span<float> send = default)
    {
        bus.Cutoff = Follow(bus.Cutoff, bus.TargetCutoff);
        bus.Filter.Process(buffer, bus.Cutoff);
        int frames = buffer.Length / AudioFormat.Channels;
        float step = frames == 0 ? 0 : (bus.TargetGain - bus.Gain) / frames;
        float gain = bus.Gain;
        for (int frame = 0; frame < frames; frame++)
        {
            gain += step;
            float left = buffer[frame * 2] * gain;
            float right = buffer[(frame * 2) + 1] * gain;
            into[frame * 2] += left;
            into[(frame * 2) + 1] += right;
            if (!send.IsEmpty)
            {
                send[frame * 2] += left;
                send[(frame * 2) + 1] += right;
            }
        }

        bus.Gain = bus.TargetGain;
    }

    private void MixVoice(Voice voice, int frames)
    {
        Bus bus = _buses[(int)voice.Bus];
        if (bus.Paused)
        {
            return;
        }

        long wait = voice.Start - _position;
        if (wait >= frames)
        {
            return;
        }

        int offset = wait > 0 ? (int)wait : 0;
        int wanted = frames - offset;
        Span<float> block = _scratch.AsSpan(0, wanted * AudioFormat.Channels);
        int read = voice.Source!.Read(block);
        Span<float> played = block[..(read * AudioFormat.Channels)];
        voice.Cutoff = Follow(voice.Cutoff, voice.TargetCutoff);
        voice.Filter.Process(played, voice.Cutoff);
        Span<float> into = bus.Buffer.AsSpan(offset * AudioFormat.Channels);
        float leftStep = (voice.TargetLeft - voice.Left) / wanted;
        float rightStep = (voice.TargetRight - voice.Right) / wanted;
        bool faded = false;
        long first = _position + offset;
        for (int frame = 0; frame < read; frame++)
        {
            voice.Left += leftStep;
            voice.Right += rightStep;
            if (voice.StopAt >= 0 && voice.FadeTotal == 0 && first + frame >= voice.StopAt)
            {
                if (voice.StopFade <= 0)
                {
                    faded = true;
                    break;
                }

                voice.FadeTotal = voice.StopFade;
                voice.FadeLeft = voice.StopFade;
            }

            float fade = 1;
            if (voice.FadeTotal > 0)
            {
                fade = voice.FadeLeft / (float)voice.FadeTotal;
                voice.FadeLeft--;
                if (voice.FadeLeft <= 0)
                {
                    faded = true;
                    break;
                }
            }

            if (voice.FadeInDone < voice.FadeInTotal)
            {
                fade *= voice.FadeInDone / (float)voice.FadeInTotal;
                voice.FadeInDone++;
            }

            into[frame * 2] += played[frame * 2] * voice.Left * fade;
            into[(frame * 2) + 1] += played[(frame * 2) + 1] * voice.Right * fade;
        }

        voice.Left = voice.TargetLeft;
        voice.Right = voice.TargetRight;
        if (faded || read < wanted)
        {
            End(voice);
        }
    }

    private void TakeCommands()
    {
        while (_commands.TryDequeue(out MixerCommand command))
        {
            switch (command.Kind)
            {
                case MixerCommandKind.Play:
                    Play(in command);
                    break;
                case MixerCommandKind.SetVoice:
                    if (Find(command.Voice) is { } set)
                    {
                        set.TargetLeft = command.Left;
                        set.TargetRight = command.Right;
                        set.TargetCutoff = command.Cutoff;
                    }

                    break;
                case MixerCommandKind.Stop:
                    Stop(in command);
                    break;
                case MixerCommandKind.StopAt:
                    StopAt(in command);
                    break;
                case MixerCommandKind.SetBus:
                    Bus bus = _buses[(int)command.Bus];
                    bus.TargetGain = command.Left;
                    bus.TargetCutoff = command.Cutoff;
                    break;
                case MixerCommandKind.SetReverb:
                    _targetWet = command.Left;
                    _reverb.Size = command.Size;
                    _reverb.Damping = command.Right;
                    break;
                case MixerCommandKind.PauseBus:
                    _buses[(int)command.Bus].Paused = command.Paused;
                    break;
            }
        }
    }

    private void Play(in MixerCommand command)
    {
        Voice? free = null;
        foreach (Voice voice in _voices)
        {
            if (!voice.Active)
            {
                free = voice;
                break;
            }
        }

        if (free is null || command.Source is null)
        {
            _finished.Enqueue(command.Voice);
            return;
        }

        free.Active = true;
        free.Id = command.Voice;
        free.Source = command.Source;
        free.Bus = command.Bus;
        free.Left = command.Left;
        free.Right = command.Right;
        free.TargetLeft = command.Left;
        free.TargetRight = command.Right;
        free.Cutoff = command.Cutoff;
        free.TargetCutoff = command.Cutoff;
        free.Filter = default;
        free.Start = command.Frames;
        free.FadeLeft = 0;
        free.FadeTotal = 0;
        free.FadeInTotal = command.More;
        free.FadeInDone = 0;
        free.StopAt = -1;
        free.StopFade = 0;
        if (command.CatchUp)
        {
            CatchUp(free);
        }
    }

    /// <summary>Reads past what a voice scheduled on a frame that has passed would have played by now.</summary>
    private void CatchUp(Voice voice)
    {
        long missed = _position - voice.Start;
        while (missed > 0)
        {
            long most = Math.Min(missed, AudioFormat.BlockFrames);
            int frames = (int)most;
            Span<float> skipped = _scratch.AsSpan(0, frames * AudioFormat.Channels);
            int read = voice.Source!.Read(skipped);
            if (read < frames)
            {
                End(voice);
                return;
            }

            missed -= frames;
        }
    }

    private void StopAt(in MixerCommand command)
    {
        Voice? voice = Find(command.Voice);
        if (voice is null)
        {
            return;
        }

        if (command.More <= voice.Start)
        {
            End(voice);
            return;
        }

        voice.StopAt = command.More;
        voice.StopFade = command.Frames;
    }

    private void Stop(in MixerCommand command)
    {
        Voice? voice = Find(command.Voice);
        if (voice is null)
        {
            return;
        }

        if (command.Frames <= 0 || voice.Start > _position)
        {
            End(voice);
            return;
        }

        if (voice.FadeTotal == 0)
        {
            voice.FadeTotal = command.Frames;
            voice.FadeLeft = command.Frames;
        }
    }

    private Voice? Find(int id)
    {
        foreach (Voice voice in _voices)
        {
            if (voice.Active && voice.Id == id)
            {
                return voice;
            }
        }

        return null;
    }

    private void End(Voice voice)
    {
        voice.Active = false;
        voice.Source = null;
        _finished.Enqueue(voice.Id);
    }

    private sealed class Voice
    {
        public bool Active;
        public int Id;
        public ISoundSource? Source;
        public AudioBus Bus;
        public float Left;
        public float Right;
        public float TargetLeft;
        public float TargetRight;
        public float Cutoff;
        public float TargetCutoff;
        public LowPass Filter;
        public long Start;
        public long FadeLeft;
        public long FadeTotal;
        public long FadeInTotal;
        public long FadeInDone;
        public long StopAt = -1;
        public long StopFade;
    }

    private sealed class Bus
    {
        public readonly float[] Buffer = new float[AudioFormat.BlockFrames * AudioFormat.Channels];
        public float Gain = 1;
        public float TargetGain = 1;
        public float Cutoff = MixerCommand.NoCutoff;
        public float TargetCutoff = MixerCommand.NoCutoff;
        public LowPass Filter;
        public bool Paused;
    }
}
