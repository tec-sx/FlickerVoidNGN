using System.Numerics;

using FlickerVoid.Editor.Core.Undo;

namespace FlickerVoid.Editor.Core.Viewports;

/// <summary>What clicks and drags in the viewport work on, and what its undo and save act on: the scene, or a creation being made.</summary>
public interface IViewportMode
{
    /// <summary>The undo steps that Undo and Redo walk while the mode is open.</summary>
    UndoHistory History { get; }

    /// <summary>A left click without Alt.</summary>
    void PointerPressed(Vector2 pixel, int width, int height);

    void PointerMoved(Vector2 pixel, int width, int height);

    void PointerReleased();

    /// <summary>Runs once a frame, after the camera moved.</summary>
    void Update();

    /// <summary>Saves what the mode edits; false when it has nowhere to save it yet.</summary>
    bool Save();
}
