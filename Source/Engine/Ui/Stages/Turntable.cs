namespace FlickerVoid.Engine.Ui.Stages;

/// <summary>Turns its entity about the up axis: on its own at <see cref="Speed"/>, and by <see cref="DragSpeed"/> as a stage is dragged.</summary>
public struct Turntable
{
    public Turntable()
    {
    }

    /// <summary>Degrees a second; 0 stands still until dragged.</summary>
    public float Speed { get; set; } = 20;

    /// <summary>Degrees a pixel of dragging turns it.</summary>
    public float DragSpeed { get; set; } = 0.5f;
}
