namespace FlickerVoid.Editor.Core.Music;

/// <summary>A music area's name chip over the viewport: its name, the tracks it asks for, and the pixel it hangs at.</summary>
public readonly record struct MusicAreaLabel(string Name, string Layers, double X, double Y);
