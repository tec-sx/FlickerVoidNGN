using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Upscaling;

/// <summary>
/// The sub-pixel jitter of frames drawn for an upscaler: the Halton (2, 3) sequence from its second point, centred on the pixel,
/// cycling through eight phases for every output pixel a render pixel covers, as DLSS asks, and at least 32 for an upscaler that
/// denoises, as Ray Reconstruction asks.
/// </summary>
public sealed class JitterSequence
{
    private const int PhasesPerPixel = 8;

    private const int DenoisingPhases = 32;

    private int _phase;

    /// <summary>How many phases frames drawn at the render width and upscaled to the output width cycle through.</summary>
    public static int Phases(int renderWidth, int outputWidth, bool denoises)
    {
        float ratio = (float)outputWidth / renderWidth;
        int phases = (int)MathF.Round(PhasesPerPixel * ratio * ratio);
        if (denoises)
        {
            return Math.Max(phases, DenoisingPhases);
        }

        return phases;
    }

    /// <summary>The next frame's jitter, in render pixels right and down, from -0.5 to 0.5.</summary>
    public Vector2 Next(int phases)
    {
        _phase = (_phase % phases) + 1;
        float right = Halton(_phase, 2) - 0.5f;
        float down = Halton(_phase, 3) - 0.5f;
        return new Vector2(right, down);
    }

    /// <summary>The index's digits in the base, mirrored about the point.</summary>
    private static float Halton(int index, int radix)
    {
        float result = 0;
        float fraction = 1;
        while (index > 0)
        {
            fraction /= radix;
            result += fraction * (index % radix);
            index /= radix;
        }

        return result;
    }
}
