using System.Text.Json;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Core.Tags;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Editor.Tests.Tags;

/// <summary>Shows a list of tags as chips, or the tag picker, in a headless editor window, over a small tree of tags.</summary>
internal sealed class TagViewsTestsBuilder
{
    private Window? _window;

    public TagCatalog Catalog { get; } = new();

    public List<Tag> Value { get; } = [new Tag("Music.Drums")];

    public TagsFieldViewModel Field { get; private set; } = null!;

    public async Task Run(Func<Control> show, Action test)
    {
        await HeadlessEditor.Session.Dispatch(
            () =>
            {
                Catalog.Add("Music.Drums.Inn");
                Catalog.Add("Music.Hats");
                Catalog.Add("Ambience.Rain");
                Field = (TagsFieldViewModel)FieldFactory.Create(new Holder(this));
                Control view = show();
                _window = new Window { Content = view, Width = 400, Height = 500 };
                _window.Show();
                Settle();
                test();
                _window.Close();
            },
            CancellationToken.None);
    }

    public static void Settle() => Dispatcher.UIThread.RunJobs();

    public IReadOnlyList<T> Find<T>()
        where T : Control
    {
        return [.. _window!.GetVisualDescendants().OfType<T>().Where(control => control.IsEffectivelyVisible)];
    }

    public void Click(Control control)
    {
        Point centre = new(control.Bounds.Width / 2, control.Bounds.Height / 2);
        Point point = control.TranslatePoint(centre, _window!)!.Value;
        _window!.MouseDown(point, MouseButton.Left);
        _window!.MouseUp(point, MouseButton.Left);
        Settle();
    }

    private sealed class Holder(TagViewsTestsBuilder owner) : IEditableValue
    {
        public string Label => "Triggers";

        public Type Type => typeof(List<Tag>);

        public bool CanWrite => true;

        public bool AcceptsNull => false;

        public JsonSerializerOptions? Options => null;

        public TAttribute? GetAttribute<TAttribute>()
            where TAttribute : Attribute
        {
            return null;
        }

        public object? Get() => owner.Value;

        public void Set(object? value)
        {
            owner.Value.Clear();
            owner.Value.AddRange((List<Tag>)value!);
        }

        public string ToJson(object? value) => "";
    }
}
