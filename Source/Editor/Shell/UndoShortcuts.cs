using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Editor.Inspector;

namespace FlickerVoid.Editor;

/// <summary>
/// Gives a window or a docked editor undo and redo over the history of the document it shows: Ctrl+Z undoes, Ctrl+Y and Ctrl+Shift+Z redo, and a
/// number box dragged in it is recorded as one step.
/// </summary>
public static class UndoShortcuts
{
    public static void Attach(Control window, Func<UndoHistory?> findHistory)
    {
        UndoHistory? dragged = null;
        window.AddHandler(InputElement.KeyDownEvent, (_, e) => HandleKeyDown(e, findHistory), RoutingStrategies.Bubble);
        window.AddHandler(DragNumberBox.DragStartedEvent, (_, _) =>
        {
            dragged = findHistory();
            dragged?.BeginGroup("Drag value");
        });
        window.AddHandler(DragNumberBox.DragCompletedEvent, (_, _) =>
        {
            dragged?.EndGroup();
            dragged = null;
        });
    }

    private static void HandleKeyDown(KeyEventArgs e, Func<UndoHistory?> findHistory)
    {
        if (e.Handled || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool undo = e.Key == Key.Z && !shift;
        bool redo = e.Key == Key.Y || (e.Key == Key.Z && shift);
        if (!undo && !redo)
        {
            return;
        }

        UndoHistory? history = findHistory();
        if (history is null)
        {
            return;
        }

        if (undo)
        {
            history.Undo();
        }
        else
        {
            history.Redo();
        }

        e.Handled = true;
    }
}
