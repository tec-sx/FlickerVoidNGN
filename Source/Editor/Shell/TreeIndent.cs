using Avalonia;
using Avalonia.Data.Converters;

namespace FlickerVoid.Editor;

/// <summary>How far a tree row is indented for its level: 14 px a level, as the theme's tree rows are.</summary>
public static class TreeIndent
{
    private const double Step = 14;

    public static FuncValueConverter<int, Thickness> FromLevel { get; } = new(static level => new Thickness(level * Step, 0, 0, 0));
}
