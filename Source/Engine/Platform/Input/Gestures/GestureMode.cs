namespace FlickerVoid.Engine.Platform.Input.Gestures;

/// <summary>How a gesture completes.</summary>
public enum GestureMode
{
    /// <summary>Completes at once on press.</summary>
    Press,

    /// <summary>Completes after the key is held for the duration.</summary>
    Hold,

    /// <summary>Completes after the press count, within the duration when it's above 0.</summary>
    Mash,

    /// <summary>Completes after being focused for the duration, no key needed.</summary>
    Hover,

    /// <summary>Completes as soon as the offer becomes available.</summary>
    Automatic,
}
