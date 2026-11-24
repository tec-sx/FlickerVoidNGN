using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Undo;

namespace FlickerVoid.Editor;

public sealed partial class MainWindowViewModel
{
    /// <summary>
    /// The history Ctrl+Z works on: the mood's or the data asset's when the keyboard is in the View or Data tab, else the scene's.
    /// </summary>
    public UndoHistory? GetHistory(bool sidePanelFocused)
    {
        if (sidePanelFocused && SideTab == ViewTab)
        {
            if (_mood is null)
            {
                return null;
            }

            return _content.GetHistory(_mood.Path);
        }

        if (sidePanelFocused && SideTab == DataTab)
        {
            if (_dataPath is null)
            {
                return null;
            }

            return _content.GetHistory(_dataPath);
        }

        return Viewport.Scene.History;
    }

    /// <summary>The top bar's undo tooltip, naming the step it undoes.</summary>
    public string UndoTip => DescribeStep("Undo", GetHistory(SidePanelFocused)?.UndoName, "Ctrl+Z");

    /// <summary>The top bar's redo tooltip, naming the step it redoes.</summary>
    public string RedoTip => DescribeStep("Redo", GetHistory(SidePanelFocused)?.RedoName, "Ctrl+Y");

    /// <summary>Undoes the last step of what the keyboard is in, as Ctrl+Z does.</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void Undo() => GetHistory(SidePanelFocused)?.Undo();

    /// <summary>Redoes the last undone step of what the keyboard is in, as Ctrl+Y does.</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    public void Redo() => GetHistory(SidePanelFocused)?.Redo();

    private bool CanUndo() => GetHistory(SidePanelFocused)?.CanUndo == true;

    private bool CanRedo() => GetHistory(SidePanelFocused)?.CanRedo == true;

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
        UndoHistory? history = GetHistory(SidePanelFocused);
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
