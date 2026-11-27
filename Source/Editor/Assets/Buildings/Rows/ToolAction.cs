using CommunityToolkit.Mvvm.Input;

namespace FlickerVoid.Editor.Assets.Buildings.Rows;

/// <summary>A button of an <see cref="ActionsRow"/>: its name, its icon's name, and what it does.</summary>
public sealed class ToolAction(string name, string icon, Action run, bool accent = false)
{
    public string Name { get; } = name;

    public string Icon { get; } = icon;

    public bool IsAccent { get; } = accent;

    public IRelayCommand Command { get; } = new RelayCommand(run);
}
