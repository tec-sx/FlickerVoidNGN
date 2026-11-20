using System.Windows.Input;

namespace FlickerVoid.Editor.Assets;

/// <summary>One entry of a New submenu, such as a data asset type, with the command that creates it.</summary>
public sealed record AssetChoice(string Name, ICommand Command);
