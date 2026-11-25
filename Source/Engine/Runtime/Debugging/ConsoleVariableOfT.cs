using System.Globalization;

namespace FlickerVoid.Engine.Runtime.Debugging;

/// <summary>A console variable holding a value of one type, read from text by its parser; <see cref="ConsoleVariable.Create"/> makes the usual ones.</summary>
public sealed class ConsoleVariable<T>(string name, string help, T value, ConsoleParser<T> parse) : ConsoleVariable(name, help)
{
    private T _value = value;

    public event Action<T>? Changed;

    public T Default { get; } = value;

    public T Value
    {
        get => _value;
        set
        {
            _value = value;
            Changed?.Invoke(value);
        }
    }

    public override string Text => Format(Value);

    public override string DefaultText => Format(Default);

    public override bool IsDefault => EqualityComparer<T>.Default.Equals(Value, Default);

    public override bool TrySet(string text)
    {
        if (!parse(text, out T? parsed))
        {
            return false;
        }

        Value = parsed;
        return true;
    }

    public override void Reset()
    {
        Value = Default;
    }

    public override string DescribeInvalid(string text)
    {
        if (Choices.Count > 0)
        {
            string choices = string.Join(", ", Choices);
            return $"'{text}' is not one of {choices} for {Name}.";
        }

        return $"'{text}' is not a valid {typeof(T).Name} for {Name}.";
    }

    public override string Execute(string arguments)
    {
        if (arguments.Length > 0 && !TrySet(arguments))
        {
            return DescribeInvalid(arguments);
        }

        return $"{Name} = {Text}";
    }

    private static string Format(T value)
    {
        if (value is bool flag)
        {
            return flag ? "true" : "false";
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return value?.ToString() ?? "";
    }
}
