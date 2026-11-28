using System.ComponentModel.DataAnnotations;

namespace FlickerVoid.Engine.Audio.Music;

/// <summary>A named stretch of a song, in bars; sections follow one another from the cue's start and send a <see cref="MusicSectionEvent"/> as the music enters each.</summary>
public sealed class MusicSection
{
    public string Name { get; set; } = string.Empty;

    [Range(1, 256)]
    public int Bars { get; set; } = 4;
}
