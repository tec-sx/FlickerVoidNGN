using System.Collections.ObjectModel;

namespace FlickerVoid.Editor.Core.Toasts;

/// <summary>
/// The warnings shown over the viewport when something asked for can't be done, each saying why and what to do: the newest last, a
/// few at most, each gone after a few seconds or a click, and one said again shown once.
/// </summary>
public sealed class ToastsViewModel
{
    /// <summary>How long a warning is shown.</summary>
    public const float Seconds = 6;

    /// <summary>How many are shown at once; an older one makes way.</summary>
    public const int Most = 3;

    public ObservableCollection<ToastViewModel> Items { get; } = [];

    /// <summary>Shows the warning, or shows it for longer when it is on screen already.</summary>
    public void Warn(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ToastViewModel? shown = Items.FirstOrDefault(item => item.Message == message);
        if (shown is not null)
        {
            shown.Left = Seconds;
            return;
        }

        while (Items.Count >= Most)
        {
            Items.RemoveAt(0);
        }

        ToastViewModel added = new(message, Dismiss) { Left = Seconds };
        Items.Add(added);
    }

    /// <summary>Runs once a frame: takes away the warnings whose time is up.</summary>
    public void Update(float seconds)
    {
        for (int index = Items.Count - 1; index >= 0; index--)
        {
            ToastViewModel item = Items[index];
            item.Left -= seconds;
            if (item.Left <= 0)
            {
                Items.RemoveAt(index);
            }
        }
    }

    private void Dismiss(ToastViewModel item) => Items.Remove(item);
}
