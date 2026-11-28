namespace FlickerVoid.Editor.Core.Music;

/// <summary>A section of the song laid on the ruler, in bars from the cue's start.</summary>
public readonly record struct SectionBand(string Name, double Start, double Length, int Index);
