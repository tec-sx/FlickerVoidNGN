using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Interface.Hub;

/// <summary>One page of the hub, filled from the protagonist's state when it is shown and kept up while it shows.</summary>
public abstract partial class HubPageViewModel(string title) : ObservableObject
{
    public string Title { get; } = title;

    [ObservableProperty]
    public partial bool IsSelected { get; private set; }

    /// <summary>Whose state the page shows while it is selected; <see cref="Entity.Null"/> otherwise.</summary>
    protected Entity Owner { get; private set; } = Entity.Null;

    public void Show(Entity owner)
    {
        Owner = owner;
        IsSelected = true;
        Refresh();
    }

    public void Hide()
    {
        Owner = Entity.Null;
        IsSelected = false;
    }

    protected abstract void Refresh();

    /// <summary>Refreshes for a change to <paramref name="changed"/> when the page shows its state.</summary>
    protected void RefreshFor(Entity changed)
    {
        if (IsSelected && changed == Owner)
        {
            Refresh();
        }
    }
}
