using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FlickerVoid.Editor.Assets.Buildings.Rows;

/// <summary>One option of a <see cref="ChoiceRow"/>, lit while chosen.</summary>
public sealed partial class ChoiceOption(string name, Action choose) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    public partial bool IsChosen { get; set; }

    [RelayCommand]
    private void Choose() => choose();
}
