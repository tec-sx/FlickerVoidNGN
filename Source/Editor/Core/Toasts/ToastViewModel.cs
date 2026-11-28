using CommunityToolkit.Mvvm.Input;

namespace FlickerVoid.Editor.Core.Toasts;

/// <summary>One warning shown over the viewport until its time is up or it is clicked away.</summary>
public sealed class ToastViewModel
{
    public ToastViewModel(string message, Action<ToastViewModel> dismiss)
    {
        Message = message;
        DismissCommand = new RelayCommand(() => dismiss(this));
    }

    public string Message { get; }

    public IRelayCommand DismissCommand { get; }

    /// <summary>The seconds it is still shown for.</summary>
    internal float Left { get; set; }
}
