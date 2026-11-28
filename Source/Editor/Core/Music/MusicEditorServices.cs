using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>What the music editor asks of the editor around it: the preview to listen through, the game's areas, the project's files and its mix.</summary>
public sealed class MusicEditorServices
{
    /// <summary>Opens the preview game the first time a cue is played; null where nothing can be played.</summary>
    public Func<MusicPreview?>? Preview { get; init; }

    /// <summary>The music areas of the open scene and what they ask for.</summary>
    public Func<IReadOnlyList<RehearsalSource>> Areas { get; init; } = () => [];

    /// <summary>The content paths of the sound files and sounds of the project.</summary>
    public Func<IReadOnlyList<string>> Sounds { get; init; } = () => [];

    /// <summary>The content paths of the music cues of the project.</summary>
    public Func<IReadOnlyList<string>> Cues { get; init; } = () => [];

    /// <summary>Copies sound files from the disk into the project's Content and returns their content paths.</summary>
    public Func<IReadOnlyList<string>, IReadOnlyList<string>>? Import { get; init; }

    /// <summary>The frames the preview's sound device takes at a time.</summary>
    public Func<int> BufferFrames { get; init; } = () => MusicPreview.DefaultBufferFrames;

    /// <summary>Keeps the buffer chosen and opens the preview's device with it.</summary>
    public Action<int>? SetBufferFrames { get; init; }

    /// <summary>The height of the dock under the lanes.</summary>
    public Func<double> DockHeight { get; init; } = () => MusicEditorViewModel.DefaultDockHeight;

    /// <summary>Keeps the dock's height as its grip drags it.</summary>
    public Action<double>? SetDockHeight { get; init; }

    public Func<MixSet> Mix { get; init; } = MixSet.Default;

    /// <summary>Opens the project's mix file in the data editor, making it first when the project has none.</summary>
    public Action? OpenMix { get; init; }

    public Action<string>? Open { get; init; }
}
