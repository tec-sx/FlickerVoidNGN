using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FlickerVoid.Engine.Runtime.Debugging;

/// <summary>
/// A console variable: typing its name shows the value, and typing a value after the name sets it. Saved ones are what settings
/// files keep, when they differ from their defaults.
/// </summary>
public abstract class ConsoleVariable(string name, string help) : ConsoleEntry(name, help)
{
    private static readonly string[] TrueWords = ["1", "on", "true", "yes"];
    private static readonly string[] FalseWords = ["0", "off", "false", "no"];

    /// <summary>Whether settings files keep the value; debug views and other passing switches are not saved.</summary>
    public bool Saved { get; init; } = true;

    /// <summary>The value as the console shows it and settings files hold it.</summary>
    public abstract string Text { get; }

    /// <summary>The default value as the console shows it.</summary>
    public abstract string DefaultText { get; }

    public abstract bool IsDefault { get; }

    public ConsoleValueKind Kind { get; init; }

    /// <summary>The only values the variable takes, such as an enum's names, or none when any value of its type will do.</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>Sets the value from text; false, leaving it as it was, when the text is not a value of the variable.</summary>
    public abstract bool TrySet(string text);

    public abstract void Reset();

    /// <summary>Why the text is not a value of the variable, as the console shows it.</summary>
    public abstract string DescribeInvalid(string text);

    /// <summary>A variable of any parsable type; booleans also take 1 and 0, on and off, yes and no.</summary>
    public static ConsoleVariable<T> Create<T>(string name, string help, T value, bool saved = true)
        where T : IParsable<T>
    {
        ConsoleParser<T> parse = static (string text, [MaybeNullWhen(false)] out T parsed) => T.TryParse(text, CultureInfo.InvariantCulture, out parsed);
        ConsoleValueKind kind = ConsoleValueKind.Text;
        if (typeof(T) == typeof(bool))
        {
            parse = static (string text, [MaybeNullWhen(false)] out T parsed) =>
            {
                bool found = TryParseBoolean(text, out bool flag);
                parsed = (T)(object)flag;
                return found;
            };
            kind = ConsoleValueKind.Boolean;
        }
        else if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal))
        {
            kind = ConsoleValueKind.Number;
        }

        return new ConsoleVariable<T>(name, help, value, parse) { Kind = kind, Saved = saved };
    }

    /// <summary>A variable of an enum, taking its names ignoring case, never numbers.</summary>
    public static ConsoleVariable<T> CreateEnum<T>(string name, string help, T value, bool saved = true)
        where T : struct, Enum
    {
        string[] names = Enum.GetNames<T>();
        ConsoleParser<T> parse = (string text, [MaybeNullWhen(false)] out T parsed) =>
        {
            parsed = default;
            string? match = names.FirstOrDefault(choice => string.Equals(choice, text, StringComparison.OrdinalIgnoreCase));
            return match is not null && Enum.TryParse(match, out parsed);
        };
        return new ConsoleVariable<T>(name, help, value, parse) { Choices = names, Saved = saved };
    }

    private static bool TryParseBoolean(string text, out bool value)
    {
        value = TrueWords.Contains(text, StringComparer.OrdinalIgnoreCase);
        return value || FalseWords.Contains(text, StringComparer.OrdinalIgnoreCase);
    }
}
