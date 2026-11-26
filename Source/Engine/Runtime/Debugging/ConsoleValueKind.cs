namespace FlickerVoid.Engine.Runtime.Debugging;

/// <summary>How a console variable's value is written in JSON: a boolean, a number or a string.</summary>
public enum ConsoleValueKind
{
    Text,
    Boolean,
    Number,
}
