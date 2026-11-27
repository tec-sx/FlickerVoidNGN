namespace FlickerVoid.Editor.Core.Music;

/// <summary>Something in the game that asks a cue for tracks while it holds: a music area of the open scene, with the layers it asks for.</summary>
public sealed record RehearsalSource(string Name, IReadOnlyList<string> Layers);
