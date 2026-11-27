using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FlickerVoid.Editor.Viewports;

/// <summary>One tool of the <see cref="ToolRailViewModel"/>: a name, an icon, a key and what it does, lit while it is the tool in hand.</summary>
public sealed partial class ToolItem : ObservableObject
{
    public ToolItem(string name, string? key, string icon, Action run, bool isActive)
    {
        Name = name;
        Key = key;
        Icon = icon;
        Command = new RelayCommand(run);
        IsActive = isActive;
    }

    public string Name { get; }

    /// <summary>The key that chooses the tool, as the tooltip shows it; null for none.</summary>
    public string? Key { get; }

    /// <summary>The icon's name in <see cref="LucideIcons"/>.</summary>
    public string Icon { get; }

    public IRelayCommand Command { get; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public string Tip => Key is null ? Name : $"{Name} ({Key})";
}
