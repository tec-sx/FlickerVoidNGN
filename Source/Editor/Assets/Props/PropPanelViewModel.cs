using System.Numerics;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Assets.Creators;
using FlickerVoid.Engine.Props;

namespace FlickerVoid.Editor.Assets.Props;

/// <summary>
/// The prop creator's panel: an object with no rules of its own, made of boxes added here and shaped with the mesh tools, and a part
/// taken out; doors come from the door tool.
/// </summary>
public sealed partial class PropPanelViewModel : CreatorPanelViewModel
{
    private readonly PropDocument _document;

    public PropPanelViewModel(PropDocument document)
        : base(document)
    {
        _document = document;
        Show();
    }

    public override string Kind => "Prop";

    /// <summary>The size of the next box, in metres.</summary>
    [ObservableProperty]
    public partial double Width { get; set; } = 1;

    [ObservableProperty]
    public partial double Height { get; set; } = 1;

    [ObservableProperty]
    public partial double Depth { get; set; } = 1;

    /// <summary>The prop's own parts, which can be taken out; doors are not among them.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Boxes { get; private set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    public partial string? Chosen { get; set; }

    [RelayCommand]
    public void AddBox()
    {
        Vector3 size = new((float)Math.Max(Width, 0.01), (float)Math.Max(Height, 0.01), (float)Math.Max(Depth, 0.01));
        string name = _document.AddBox(size);
        Show();
        Chosen = name;
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    public void Remove()
    {
        if (Chosen is { } name && _document.Remove(name))
        {
            Show();
        }
    }

    private bool CanRemove() => Chosen is not null && Boxes.Count > 1;

    private void Show()
    {
        PropFile prop = _document.Prop;
        string? chosen = Chosen;
        Boxes = [.. prop.Parts.Select(static part => part.Name)];
        if (chosen is not null && Boxes.Contains(chosen))
        {
            Chosen = chosen;
        }
        else
        {
            Chosen = Boxes.LastOrDefault();
        }

        RemoveCommand.NotifyCanExecuteChanged();
        ShowParts();
    }
}
