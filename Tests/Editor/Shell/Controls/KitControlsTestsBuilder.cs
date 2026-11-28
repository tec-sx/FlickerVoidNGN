using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Lists;
using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Tests.Controls;

/// <summary>Shows one of the kit's controls alone in a headless window 800 by 400, with the mouse the tests click it through.</summary>
internal sealed class KitControlsTestsBuilder
{
    private Window? _window;

    public FakeTransport Transport { get; } = new();

    public int Adds { get; private set; }

    public ICommand AddCommand => new RelayCommand(() => Adds++);

    public ObservableCollection<string> Rows { get; } = ["a", "b", "c", "d"];

    public ObservableCollection<FilterPill> Pills { get; } =
        [new FilterPill("All", 14), new FilterPill("Bass", 5, "Bass"), new FilterPill("Group", 0, IsAdd: true)];

    /// <summary>Creates the control on the headless UI thread and runs the test with it shown at the window's top left.</summary>
    public async Task Run<T>(Func<T> create, Action<T> test)
        where T : Control
    {
        await HeadlessEditor.Session.Dispatch(
            () =>
            {
                T control = create();
                StackPanel panel = new() { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
                panel.Children.Add(control);
                _window = new Window { Content = panel, Width = 800, Height = 400 };
                _window.Show();
                Settle();
                test(control);
                _window!.Close();
            },
            CancellationToken.None);
    }

    public static void Settle() => Dispatcher.UIThread.RunJobs();

    public static T Part<T>(Control owner, string name)
        where T : Control
    {
        return owner.GetVisualDescendants().OfType<T>().First(part => part.Name == name);
    }

    public void Click(Control control, MouseButton button = MouseButton.Left, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Point centre = new(control.Bounds.Width / 2, control.Bounds.Height / 2);
        Point point = control.TranslatePoint(centre, _window!)!.Value;
        _window!.MouseDown(point, button, modifiers);
        _window!.MouseUp(point, button, modifiers);
        Settle();
    }

    public void PressKey(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        _window!.KeyPressQwerty(key, modifiers);
        Settle();
    }
}
