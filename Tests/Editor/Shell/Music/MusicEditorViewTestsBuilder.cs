using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Editor.Music;
using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Audio.Serialization;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Tests.Music;

/// <summary>Shows the arrangement editor over a cue of three layers in a headless window 1200 by 700, with nothing to play it through.</summary>
internal sealed class MusicEditorViewTestsBuilder
{
    /// <summary>The options a game with audio reads its data with, so a layer's values have their types as in the editor.</summary>
    private static readonly Lazy<JsonSerializerOptions> GameOptions = new(() =>
    {
        Game game = new(NullLoggerFactory.Instance);
        game.Data.AddResolver(AudioJsonContext.Default);
        return game.Data.Options;
    });

    private Window? _window;

    public MusicEditorViewModel Editor { get; private set; } = null!;

    public MusicEditorView View { get; private set; } = null!;

    public MusicLanes Lanes => Find<MusicLanes>("Lanes");

    public ScrollViewer Scroll => Find<ScrollViewer>("LanesScroll");

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

    public static void Settle() => Dispatcher.UIThread.RunJobs();

    public T Find<T>(string name)
        where T : Control
    {
        return View.GetVisualDescendants().OfType<T>().First(control => control.Name == name);
    }

    /// <summary>The layers' headers beside the lanes, from the top.</summary>
    public IReadOnlyList<Border> Headers()
    {
        return View.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("laneHeader")).ToList();
    }

    public void Wheel(Control control, Point at, double notches, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Point point = control.TranslatePoint(at, _window!)!.Value;
        _window!.MouseWheel(point, new Vector(0, notches), modifiers);
        Settle();
    }

    /// <summary>Presses, drags and releases the left button between two points of the control.</summary>
    public void Drag(Control control, Point from, Point to)
    {
        Point start = control.TranslatePoint(from, _window!)!.Value;
        Point end = control.TranslatePoint(to, _window!)!.Value;
        _window!.MouseDown(start, MouseButton.Left);
        _window!.MouseMove(end, RawInputModifiers.LeftMouseButton);
        _window!.MouseUp(end, MouseButton.Left);
        Settle();
    }

    private void Show()
    {
        JsonSerializerOptions options = GameOptions.Value;
        JsonTypeInfo cueType = options.GetTypeInfo(typeof(MusicCue));
        JsonTypeInfo trackType = options.GetTypeInfo(typeof(MusicTrack));
        MusicCue cue = new() { Tempo = 96, Tracks = [new MusicTrack { Name = "Bass" }, new MusicTrack { Name = "Drums" }, new MusicTrack { Name = "Pad" }] };
        ClipCatalog catalog = new(_ => null);
        Editor = new MusicEditorViewModel("Music/Street.fvmusic", "Street", cue, cueType, trackType, () => { }, catalog);
        View = new MusicEditorView { DataContext = Editor };
        _window = new Window { Content = View, Width = 1200, Height = 700 };
        _window.Show();
        Settle();
    }
}
