namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>How a switch to another shot looks; every switch request carries one.</summary>
public struct ShotTransition
{
    public ShotTransition()
    {
    }

    public static ShotTransition Cut => new();

    public TransitionKind Kind { get; set; }
}
