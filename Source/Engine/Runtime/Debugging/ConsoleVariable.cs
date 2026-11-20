using System.Globalization;

namespace FlickerVoid.Engine.Runtime.Debugging;

/// <summary>A console variable: typing its name shows the value, and typing a value after the name sets it.</summary>
public sealed class ConsoleVariable<T>(string name, string help, T value) : ConsoleEntry(name, help)
    where T : IParsable<T>
{
    private T _value = value;

    public event Action<T>? Changed;

    public T Value
    {
        get => _value;
        set
        {
            _value = value;
            Changed?.Invoke(value);
        }
    }

    public override string Execute(string arguments)
    {
        if (arguments.Length > 0)
        {
            if (!T.TryParse(arguments, CultureInfo.InvariantCulture, out T? parsed))
            {
                return $"'{arguments}' is not a valid {typeof(T).Name} for {Name}.";
            }

            Value = parsed;
        }

        string shown = Format(Value);
        return $"{Name} = {shown}";
    }

    private static string Format(T value)
    {
        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return value.ToString() ?? "";
    }
}
