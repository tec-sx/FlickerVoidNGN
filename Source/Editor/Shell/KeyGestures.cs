using Avalonia.Data.Converters;
using Avalonia.Input;

namespace FlickerVoid.Editor;

/// <summary>Reads the keys view models name, such as <c>Shift+A</c>, as Avalonia's gestures.</summary>
public static class KeyGestures
{
    public static FuncValueConverter<string?, KeyGesture?> Parse { get; } = new(Read);

    private static KeyGesture? Read(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return KeyGesture.Parse(text);
    }
}
