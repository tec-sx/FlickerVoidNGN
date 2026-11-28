namespace FlickerVoid.Engine.Audio.Mixing;

/// <summary>
/// Keeps the master output within full scale without clipping it: a frame that would go over takes the gain down at once, just
/// enough to bring it to the ceiling; the gain holds a moment and then comes back up over the release. A loud mix is turned
/// down, where clamping would flatten every peak into a crackle.
/// </summary>
public sealed class Limiter
{
    /// <summary>The loudest sample let through: full scale.</summary>
    public const float Ceiling = 1;

    /// <summary>The seconds the gain holds after a peak, so the cycles of a loud tone all meet the same gain.</summary>
    public const float HoldSeconds = 0.03f;

    /// <summary>The seconds the gain takes to come most of the way back after the hold.</summary>
    public const float ReleaseSeconds = 0.12f;

    private const int HoldFrames = (int)(HoldSeconds * AudioFormat.SampleRate);

    /// <summary>A cut this small is none, so a mix the limiter let go of plays exactly as mixed.</summary>
    private const float NoCut = 1e-4f;

    /// <summary>What is left of a cut after a frame of release; the cut, not the gain, is decayed, which keeps it exact near unity.</summary>
    private static readonly float Keep = MathF.Exp(-1 / (ReleaseSeconds * AudioFormat.SampleRate));

    private float _cut;
    private float _deepest;
    private int _hold;

    /// <summary>The lowest gain since this was last asked; 1 when nothing was turned down.</summary>
    public float TakeLowest()
    {
        float deepest = _deepest;
        _deepest = _cut;
        return 1 - deepest;
    }

    /// <summary>Limits a block of interleaved stereo in place.</summary>
    public void Process(Span<float> stereo)
    {
        float cut = _cut;
        float deepest = _deepest;
        int hold = _hold;
        for (int index = 0; index + 1 < stereo.Length; index += 2)
        {
            if (hold > 0)
            {
                hold--;
            }
            else if (cut > 0)
            {
                cut *= Keep;
                if (cut < NoCut)
                {
                    cut = 0;
                }
            }

            float left = stereo[index];
            float right = stereo[index + 1];
            float peak = MathF.Max(MathF.Abs(left), MathF.Abs(right));
            float gain = 1 - cut;
            if (peak * gain > Ceiling)
            {
                gain = Ceiling / peak;
                cut = 1 - gain;
                hold = HoldFrames;
            }

            stereo[index] = left * gain;
            stereo[index + 1] = right * gain;
            deepest = MathF.Max(deepest, cut);
        }

        _cut = cut;
        _deepest = deepest;
        _hold = hold;
    }
}
