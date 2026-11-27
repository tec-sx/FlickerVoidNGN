using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Engine.Creations;

namespace FlickerVoid.Editor.Viewports;

/// <summary>The door tool's numbers in the Tool tab: each reads and writes the door being placed or changed, and shows it in the viewport at once.</summary>
public sealed partial class DoorToolViewModel(Func<CreatorMode?> creator) : ObservableObject
{
    private const float DefaultFrame = 0.08f;
    private const float DefaultWindow = 0.3f;
    private const float DefaultDepth = 0.3f;

    private DoorFit? Fit => creator()?.Door?.Fit;

    public bool IsEditing => creator()?.Door?.DoorId is not null;

    public bool IsPlaced => creator()?.Door?.IsPlaced == true;

    public double Width
    {
        get => Fit?.Width ?? 0;
        set => Write(fit => fit.Width = (float)Math.Max(value, 0.2));
    }

    public double Height
    {
        get => Fit?.Height ?? 0;
        set => Write(fit => fit.Height = (float)Math.Max(value, 0.3));
    }

    public double Thickness
    {
        get => Fit?.Thickness ?? 0;
        set => Write(fit => fit.Thickness = (float)Math.Max(value, 0.01));
    }

    public double Sill
    {
        get => Fit?.Sill ?? 0;
        set => Write(fit => fit.Sill = (float)Math.Max(value, 0));
    }

    public double Margin
    {
        get => Fit?.Margin ?? 0;
        set => Write(fit => fit.Margin = (float)Math.Max(value, 0));
    }

    public double DepthIn
    {
        get => Fit?.DepthIn ?? 0;
        set => Write(fit => fit.DepthIn = (float)Math.Max(value, 0.02));
    }

    public double DepthOut
    {
        get => Fit?.DepthOut ?? 0;
        set => Write(fit => fit.DepthOut = (float)Math.Max(value, 0.02));
    }

    public bool Through
    {
        get => Fit?.DepthIn <= 0;
        set => Write(fit => fit.DepthIn = value ? 0 : DefaultDepth);
    }

    public bool HingeLeft
    {
        get => Fit?.HingeLeft == true;
        set => Write(fit => fit.HingeLeft = value);
    }

    public bool HingeRight
    {
        get => Fit?.HingeLeft == false;
        set => Write(fit => fit.HingeLeft = !value);
    }

    public bool Outward
    {
        get => Fit?.Outward == true;
        set => Write(fit => fit.Outward = value);
    }

    public double Angle
    {
        get => Fit?.Angle ?? 0;
        set => Write(fit => fit.Angle = (float)Math.Clamp(value, 1, 170));
    }

    public bool HasFrame
    {
        get => Fit?.FrameWidth > 0;
        set => Write(fit => fit.FrameWidth = value ? DefaultFrame : 0);
    }

    public double FrameWidth
    {
        get => Fit?.FrameWidth ?? 0;
        set => Write(fit => fit.FrameWidth = (float)Math.Max(value, 0.01));
    }

    public bool HasWindow
    {
        get => Fit?.HasWindow == true;
        set => Write(fit => fit.WindowWidth = value ? DefaultWindow : 0);
    }

    public double WindowWidth
    {
        get => Fit?.WindowWidth ?? 0;
        set => Write(fit => fit.WindowWidth = (float)Math.Max(value, 0.05));
    }

    public double WindowHeight
    {
        get => Fit?.WindowHeight ?? 0;
        set => Write(fit => fit.WindowHeight = (float)Math.Max(value, 0.05));
    }

    public double WindowFrom
    {
        get => Fit?.WindowFrom ?? 0;
        set => Write(fit => fit.WindowFrom = (float)Math.Max(value, 0));
    }

    public bool Glass
    {
        get => Fit?.Glass == true;
        set => Write(fit => fit.Glass = value);
    }

    public double GrabHeight
    {
        get => Fit?.GrabHeight ?? 0;
        set => Write(fit => fit.GrabHeight = (float)Math.Max(value, 0));
    }

    public double GrabInset
    {
        get => Fit?.GrabInset ?? 0;
        set => Write(fit => fit.GrabInset = (float)Math.Max(value, 0));
    }

    /// <summary>Reads every number again, after the door or a handle changed them.</summary>
    public void Refresh() => OnPropertyChanged(string.Empty);

    /// <summary>Takes the placed door being changed out of the creation.</summary>
    [RelayCommand]
    public void Remove() => creator()?.RemoveDoor();

    private void Write(Action<DoorFit> change)
    {
        if (creator()?.Door is not { } door)
        {
            return;
        }

        change(door.Fit);
        door.Touch();
        Refresh();
    }
}
