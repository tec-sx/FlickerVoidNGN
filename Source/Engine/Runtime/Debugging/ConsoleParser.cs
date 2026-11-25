using System.Diagnostics.CodeAnalysis;

namespace FlickerVoid.Engine.Runtime.Debugging;

/// <summary>Reads a console variable's value from typed text.</summary>
public delegate bool ConsoleParser<T>(string text, [MaybeNullWhen(false)] out T value);
