using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FlickerVoid.Editor.Flows;

/// <summary>The theme's brushes for a node kind: its header's tint and its icon's colour.</summary>
public static class FlowNodeBrushes
{
    public static FuncValueConverter<string?, IBrush?> Header { get; } = new(kind => Find($"Flow{kind}HeaderBrush"));

    public static FuncValueConverter<string?, IBrush?> Icon { get; } = new(kind => Find($"Flow{kind}IconBrush"));

    private static IBrush? Find(string key)
    {
        if (Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out object? found))
        {
            return found as IBrush;
        }

        return null;
    }
}
