using Avalonia;
using Avalonia.Media;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>The theme's colours a timeline control draws with, read once per frame, with the colour of what it draws now.</summary>
public readonly record struct StyledColors(
    Color Tint,
    Color Deep,
    Color Base,
    Color Panel,
    Color Line,
    Color Text,
    Color MutedText,
    Color Hint,
    Color Accent,
    Color AccentInk,
    Color Ink)
{
    public static StyledColors Read(StyledElement element)
    {
        return new StyledColors(
            ThemeColors.Find(element, "EditorTrack1"),
            ThemeColors.Find(element, "EditorDeepBrush"),
            ThemeColors.Find(element, "EditorFrameBrush"),
            ThemeColors.Find(element, "EditorPanelBrush"),
            ThemeColors.Find(element, "EditorLineBrush"),
            ThemeColors.Find(element, "EditorTextBrush"),
            ThemeColors.Find(element, "EditorMutedTextBrush"),
            ThemeColors.Find(element, "EditorHintBrush"),
            ThemeColors.Find(element, "EditorAccentBrush"),
            ThemeColors.Find(element, "EditorAccentInkBrush"),
            ThemeColors.Find(element, "EditorCueInk"));
    }

    public StyledColors WithTint(Color tint) => this with { Tint = tint };
}
