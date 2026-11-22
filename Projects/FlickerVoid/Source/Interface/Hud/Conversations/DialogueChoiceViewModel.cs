using CommunityToolkit.Mvvm.Input;

namespace FlickerVoid.Interface.Hud.Conversations;

/// <summary>A choice the dialogue box offers, numbered from 1, locked with its reason when its conditions fail.</summary>
public sealed class DialogueChoiceViewModel(int number, string text, bool available, string lockedReason, Action choose)
{
    public string Label { get; } = $"{number}. {text}";

    public bool IsAvailable { get; } = available;

    public string LockedReason { get; } = lockedReason;

    public bool IsLocked => LockedReason.Length > 0;

    public IRelayCommand ChooseCommand { get; } = new RelayCommand(choose, () => available);
}
