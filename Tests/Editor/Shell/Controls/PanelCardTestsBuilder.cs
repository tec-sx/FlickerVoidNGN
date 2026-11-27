using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FlickerVoid.Editor.Controls;

namespace FlickerVoid.Editor.Tests.Controls;

/// <summary>Shows a panel card with two rows in a headless window of the editor, with the mouse the tests click it through.</summary>
internal sealed class PanelCardTestsBuilder
{
    private Window? _window;
    private StackPanel? _panel;

    public PanelCard Card { get; private set; } = null!;

    /// <summary>The row with the short label, "Size".</summary>
    public PanelRow Short { get; private set; } = null!;

    /// <summary>A label wider than the 96 px a label column starts at, which fits the side panel at its default width.</summary>
    public const string LongLabel = "Longer label";

    /// <summary>The row with <see cref="LongLabel"/>.</summary>
    public PanelRow Long { get; private set; } = null!;

    /// <summary>Runs <paramref name="test"/> on the headless UI thread with the card shown, open, 300 px wide.</summary>
    public async Task Run(Action test)
    {
        await HeadlessEditor.Session.Dispatch(
            () =>
            {
                Show();
                test();
                _window!.Close();
            },
            CancellationToken.None);
    }

    /// <summary>Lets the layout catch up with a change, as the next frame would.</summary>
    public static void Settle() => Dispatcher.UIThread.RunJobs();

    /// <summary>The part of the card's own look with the name, such as its header.</summary>
    public T Part<T>(string name)
        where T : Control
    {
        return Part<T>(Card, name);
    }

    public static T Part<T>(Control owner, string name)
        where T : Control
    {
        return owner.GetVisualDescendants().OfType<T>().First(part => part.Name == name && part.TemplatedParent == owner);
    }

    public void Click(Control control)
    {
        Point centre = new(control.Bounds.Width / 2, control.Bounds.Height / 2);
        Point point = control.TranslatePoint(centre, _window!)!.Value;
        _window!.MouseDown(point, MouseButton.Left);
        _window!.MouseUp(point, MouseButton.Left);
        Settle();
    }

    /// <summary>Where the control's left edge is, across the window.</summary>
    public double LeftOf(Control control)
    {
        return control.TranslatePoint(default, _window!)!.Value.X;
    }

    /// <summary>Whether the text ends in an ellipsis for want of room.</summary>
    public static bool IsCut(TextBlock text)
    {
        return text.TextLayout.TextLines.Any(line => line.HasCollapsed);
    }

    /// <summary>Makes the panel the card is in as wide as a drag of its edge would.</summary>
    public void Resize(double width)
    {
        _panel!.Width = width;
        Settle();
    }

    private void Show()
    {
        Short = new PanelRow { Label = "Size", Content = new TextBox { Text = "2" } };
        Long = new PanelRow { Label = LongLabel, Content = new TextBox { Text = "3" } };
        StackPanel rows = new();
        rows.Children.Add(Short);
        rows.Children.Add(Long);
        Card = new PanelCard { Header = "Greybox", Content = rows };
        _panel = new StackPanel { Width = 300 };
        _panel.Children.Add(Card);
        _window = new Window { Content = _panel, Width = 400, Height = 300 };
        _window.Show();
        Settle();
    }
}
