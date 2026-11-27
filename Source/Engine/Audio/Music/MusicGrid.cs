namespace FlickerVoid.Engine.Audio.Music;

/// <summary>The lines of a cue's grid, counted in bars from the cue's start; the music system and the editor's arrangement agree on them here.</summary>
public static class MusicGrid
{
    /// <summary>The bars between two lines of the quantize: a beat, a bar or a phrase; 0 for none.</summary>
    public static double GetUnit(MusicCue cue, MusicQuantize quantize)
    {
        switch (quantize)
        {
            case MusicQuantize.Beat:
                return 1.0 / cue.BeatsPerBar;
            case MusicQuantize.Bar:
                return 1;
            case MusicQuantize.Phrase:
                return cue.PhraseBars;
            default:
                return 0;
        }
    }

    /// <summary>The first line, a whole number of units from the start, on or after a position; the position itself without a unit.</summary>
    public static double NextLine(double position, double unit)
    {
        if (unit <= 0)
        {
            return position;
        }

        double lines = Math.Ceiling((position / unit) - 0.000001);
        lines = Math.Max(0, lines);
        return lines * unit;
    }
}
