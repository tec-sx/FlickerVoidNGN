namespace FlickerVoid.Engine.Audio.Mixing;

/// <summary>
/// A room's reverberation in the Freeverb design (Jezar, public domain): eight low-passed feedback combs and four all-pass filters
/// per ear, the right ear's delays a little longer so the two differ. <see cref="Process"/> neither allocates nor locks.
/// </summary>
public sealed class Reverb
{
    private static readonly int[] CombTunings = [1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617];
    private static readonly int[] AllPassTunings = [556, 441, 341, 225];

    private const int StereoSpread = 23;
    private const float FixedGain = 0.015f;
    private const float AllPassFeedback = 0.5f;

    private readonly Delay[] _combsLeft = new Delay[CombTunings.Length];
    private readonly Delay[] _combsRight = new Delay[CombTunings.Length];
    private readonly Delay[] _allPassLeft = new Delay[AllPassTunings.Length];
    private readonly Delay[] _allPassRight = new Delay[AllPassTunings.Length];
    private float _feedback = 0.84f;
    private float _damp = 0.2f;

    public Reverb()
    {
        double scale = AudioFormat.SampleRate / 44100.0;
        for (int index = 0; index < CombTunings.Length; index++)
        {
            _combsLeft[index] = new Delay((int)(CombTunings[index] * scale));
            _combsRight[index] = new Delay((int)((CombTunings[index] + StereoSpread) * scale));
        }

        for (int index = 0; index < AllPassTunings.Length; index++)
        {
            _allPassLeft[index] = new Delay((int)(AllPassTunings[index] * scale));
            _allPassRight[index] = new Delay((int)((AllPassTunings[index] + StereoSpread) * scale));
        }
    }

    /// <summary>How big the room is, from 0 (a closet, a short tail) to 1 (a hall, seconds of tail).</summary>
    public float Size
    {
        get => (_feedback - 0.7f) / 0.28f;
        set => _feedback = (Math.Clamp(value, 0, 1) * 0.28f) + 0.7f;
    }

    /// <summary>How much the walls soak up high sounds, from 0 (bright) to 1 (dull).</summary>
    public float Damping
    {
        get => _damp;
        set => _damp = Math.Clamp(value, 0, 1) * 0.9f;
    }

    /// <summary>Adds the room's response to the interleaved stereo <paramref name="input"/> into <paramref name="output"/>, at <paramref name="wet"/>.</summary>
    public void Process(ReadOnlySpan<float> input, Span<float> output, float wet)
    {
        int frames = input.Length / AudioFormat.Channels;
        for (int frame = 0; frame < frames; frame++)
        {
            float mono = (input[frame * 2] + input[(frame * 2) + 1]) * FixedGain;
            float left = 0;
            float right = 0;
            for (int index = 0; index < _combsLeft.Length; index++)
            {
                left += _combsLeft[index].Comb(mono, _feedback, _damp);
                right += _combsRight[index].Comb(mono, _feedback, _damp);
            }

            for (int index = 0; index < _allPassLeft.Length; index++)
            {
                left = _allPassLeft[index].AllPass(left, AllPassFeedback);
                right = _allPassRight[index].AllPass(right, AllPassFeedback);
            }

            output[frame * 2] += left * wet * 3;
            output[(frame * 2) + 1] += right * wet * 3;
        }
    }

    private sealed class Delay(int length)
    {
        private readonly float[] _buffer = new float[Math.Max(1, length)];
        private int _index;
        private float _store;

        public float Comb(float input, float feedback, float damp)
        {
            float output = _buffer[_index];
            _store = (output * (1 - damp)) + (_store * damp);
            _buffer[_index] = input + (_store * feedback);
            Advance();
            return output;
        }

        public float AllPass(float input, float feedback)
        {
            float buffered = _buffer[_index];
            float output = buffered - input;
            _buffer[_index] = input + (buffered * feedback);
            Advance();
            return output;
        }

        private void Advance()
        {
            _index++;
            if (_index >= _buffer.Length)
            {
                _index = 0;
            }
        }
    }
}
