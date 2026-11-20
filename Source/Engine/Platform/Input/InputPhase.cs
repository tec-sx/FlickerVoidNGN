namespace FlickerVoid.Engine.Platform.Input;

/// <summary>What happened to an input action this frame.</summary>
public enum InputPhase
{
    /// <summary>The action's first binding went down this frame.</summary>
    Pressed,

    /// <summary>The action's last binding came up this frame.</summary>
    Released,

    /// <summary>The input was aborted without a commit, such as when the window lost focus.</summary>
    Cancelled,
}
