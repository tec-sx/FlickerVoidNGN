using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Audio.Music;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>A notifier of the cue as a row of the Cue tab: its name, which gameplay hears, and the bar of the phrase it sits on.</summary>
public sealed partial class NotifierViewModel : ObservableObject
{
    private readonly Action _edited;

    public NotifierViewModel(MusicNotify notify, Action edited)
    {
        Notify = notify;
        _edited = edited;
    }

    public MusicNotify Notify { get; }

    public string Name
    {
        get => Notify.Name;
        set
        {
            Notify.Name = value;
            OnPropertyChanged();
            _edited();
        }
    }

    public decimal Bar
    {
        get => (decimal)Notify.Bar;
        set
        {
            Notify.Bar = (float)Math.Max(1, value);
            OnPropertyChanged();
            _edited();
        }
    }

    public bool Repeat
    {
        get => Notify.Repeat;
        set
        {
            Notify.Repeat = value;
            OnPropertyChanged();
            _edited();
        }
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Repeat));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Bar));
    }
}
