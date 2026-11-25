using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Undo;

namespace FlickerVoid.Editor;

public sealed partial class MainWindowViewModel
{
    /// <summary>
    /// The history Ctrl+Z works on: the mood's when the keyboard is in the outliner's View tab, the data asset's when it is in the
    /// context panel's Data tab, else the viewport's: the scene's, or in the creator the creation file's.
    /// </summary>
    public UndoHistory? GetHistory()
    {
        if (OutlinerFocused && OutlinerTab == ViewTab)
        {
            if (_mood is null)
            {
                return null;
            }

            return _content.GetHistory(_mood.Path);
        }

        if (SidePanelFocused && SideTab == DataTab)
        {
            if (_dataPath is null)
            {
                return null;
            }

            return _content.GetHistory(_dataPath);
        }

        return Viewport.Mode.History;
    }

    /// <summary>The top bar's undo tooltip, naming the step it undoes.</summary>
    public string UndoTip => DescribeStep("Undo", GetHistory()?.UndoName, "Ctrl+Z");

    /// <summary>The top bar's redo tooltip, naming the step it redoes.</summary>
    public string RedoTip => DescribeStep("Redo", GetHistory()?.RedoName, "Ctrl+Y");

    /// <summary>Undoes the last step of what the keyboard is in, as Ctrl+Z does.</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void Undo() => GetHistory()?.Undo();

    /// <summary>Redoes the last undone step of what the keyboard is in, as Ctrl+Y does.</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    public void Redo() => GetHistory()?.Redo();

    private bool CanUndo() => GetHistory()?.CanUndo == true;

    private bool CanRedo() => GetHistory()?.CanRedo == true;

    private static string DescribeStep(string action, string? step, string key)
    {
        if (step is null)
        {
            return $"{action} ({key}): nothing to {action.ToLowerInvariant()}.";
        }

        return $"{action} {step} ({key}).";
    }

    /// <summary>Follows the history the top bar's undo and redo work on, so their state and tooltips stay current.</summary>
    private void WatchHistory()
    {
        UndoHistory? history = GetHistory();
        if (history == _watchedHistory)
        {
            RefreshUndo();
            return;
        }

        if (_watchedHistory is not null)
        {
            _watchedHistory.Changed -= RefreshUndo;
        }

        _watchedHistory = history;
        if (history is not null)
        {
            history.Changed += RefreshUndo;
        }

        RefreshUndo();
    }

    private void RefreshUndo()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(UndoTip));
        OnPropertyChanged(nameof(RedoTip));
    }
}
