using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;

using FlickerVoid.Editor.Core.Tags;

namespace FlickerVoid.Editor.Tags;

/// <summary>The project's tags to pick from; Enter adds the typed tag or ends a rename, Escape gives a rename up.</summary>
public partial class TagPickerView : UserControl
{
    public TagPickerView()
    {
        InitializeComponent();
    }

    /// <summary>A row's indent as its left margin.</summary>
    public static FuncValueConverter<double, Thickness> LeftIndent { get; } = new(indent => new Thickness(indent, 0, 0, 0));

    /// <summary>Hides a fold chevron a leaf has no use for, keeping its room.</summary>
    public static FuncValueConverter<bool, double> Shown { get; } = new(shown => shown ? 1 : 0);

    private void HandleNewTagKey(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter && DataContext is TagPickerViewModel picker)
        {
            picker.AddTagCommand.Execute(null);
            args.Handled = true;
        }
    }

    private void HandleAddBelow(object? sender, RoutedEventArgs args)
    {
        NewTagText.Focus();
        NewTagText.CaretIndex = NewTagText.Text?.Length ?? 0;
    }

    private void HandleRenameKey(object? sender, KeyEventArgs args)
    {
        if (sender is not TextBox { DataContext: TagPickerRowViewModel row } || DataContext is not TagPickerViewModel picker)
        {
            return;
        }

        if (args.Key == Key.Enter)
        {
            picker.CommitRenameCommand.Execute(row);
            args.Handled = true;
        }
        else if (args.Key == Key.Escape)
        {
            picker.CancelRenameCommand.Execute(row);
            args.Handled = true;
        }
    }

    private void HandleRenameShown(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (sender is TextBox box)
        {
            box.PropertyChanged += (_, change) =>
            {
                if (change.Property == IsVisibleProperty && box.IsVisible)
                {
                    box.Focus();
                    box.SelectAll();
                }
            };
        }
    }
}
