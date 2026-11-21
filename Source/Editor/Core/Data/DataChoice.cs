using System.Windows.Input;

namespace FlickerVoid.Editor.Core.Data;

/// <summary>A type an item added to a data list may be, with the command that adds it.</summary>
public sealed record DataChoice(string Name, ICommand Command);
