using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Assets.Flows;

/// <summary>What a new flow node class is called and which code folder it goes in, asked before the browser writes it.</summary>
public sealed partial class FlowNodePrompt(IReadOnlyList<string> folders, string folder) : ObservableObject
{
    /// <summary>The code folders, below the code root ("Gameplay/World/Doors").</summary>
    public IReadOnlyList<string> Folders { get; } = folders;

    [ObservableProperty]
    public partial string Folder { get; set; } = folder;

    [ObservableProperty]
    public partial string Name { get; set; } = "";
}
