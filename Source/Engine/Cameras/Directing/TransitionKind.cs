namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>How the director switches from one shot to the next.</summary>
public enum TransitionKind
{
    /// <summary>The next shot is live on the next frame.</summary>
    Cut,

    /// <summary>The old shot's live view dissolves into the new shot over the duration.</summary>
    CrossFade,

    /// <summary>The world fades to the colour over the duration, the next shot goes live, and it fades back in over the in duration.</summary>
    Fade,

    /// <summary>The camera moves from where it is to the next shot over the duration, eased, so rails chain.</summary>
    Blend,
}
