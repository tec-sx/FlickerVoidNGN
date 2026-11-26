using Avalonia.Data.Converters;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Inspector;

namespace FlickerVoid.Editor.Inspector;

/// <summary>The icons of the inspector's category rail: a module's name to its icon.</summary>
public static class CategoryIcons
{
    public static FuncValueConverter<string?, Geometry?> ByModule { get; } = new(Find);

    private static Geometry? Find(string? module)
    {
        if (module is null)
        {
            return null;
        }

        string icon = ComponentChoices.GetModuleIcon(module);
        return LucideIcons.Find(icon);
    }
}
