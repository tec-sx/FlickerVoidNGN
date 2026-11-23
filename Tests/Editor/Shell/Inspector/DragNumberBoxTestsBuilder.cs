using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

using FlickerVoid.Editor.Inspector;

namespace FlickerVoid.Editor.Tests.Inspector;

/// <summary>Shows two number boxes in a headless window, with the mouse, keys and clipboard the tests drive them through.</summary>
internal sealed class DragNumberBoxTestsBuilder
{
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(TestApplication)));

    private Window? _window;

    public DragNumberBox First { get; private set; } = null!;

    public DragNumberBox Second { get; private set; } = null!;

    /// <summary>Runs <paramref name="test"/> on the headless UI thread with the boxes showing 1.5 and 7.</summary>
    public void Run(Func<Task> test)
    {
        Task run = Session.Value.Dispatch(
            async () =>
            {
                Show();
                await test();
                _window!.Close();
            },
            CancellationToken.None);
        run.GetAwaiter().GetResult();
    }

    public async Task SetClipboard(string text)
    {
        await ClipboardExtensions.SetTextAsync(_window!.Clipboard!, text);
    }

    public async Task<string?> GetClipboard()
    {
        Dispatcher.UIThread.RunJobs();
        return await ClipboardExtensions.TryGetTextAsync(_window!.Clipboard!);
    }

    public void Click(DragNumberBox box, MouseButton button)
    {
        Point point = GetCentre(box);
        _window!.MouseDown(point, button);
        _window!.MouseUp(point, button);
        Dispatcher.UIThread.RunJobs();
    }

    public void PressCopy()
    {
        _window!.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
    }

    private void Show()
    {
        First = new DragNumberBox { Value = 1.5 };
        Second = new DragNumberBox { Value = 7 };
        StackPanel boxes = new() { Width = 120 };
        boxes.Children.Add(First);
        boxes.Children.Add(Second);
        _window = new Window { Content = boxes, Width = 200, Height = 200 };
        _window.Show();
        Dispatcher.UIThread.RunJobs();
    }

    private Point GetCentre(DragNumberBox box)
    {
        Point centre = new(box.Bounds.Width / 2, box.Bounds.Height / 2);
        return box.TranslatePoint(centre, _window!)!.Value;
    }

    /// <summary>The Fluent look the text box's context menu comes from.</summary>
    private sealed class TestApplication : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
        }
    }
}
