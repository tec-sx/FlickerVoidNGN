namespace FlickerVoid.Engine.Abilities.Definitions;

/// <summary>When an ability activates on its own.</summary>
public enum AbilityPolicy : byte
{
    /// <summary>When its input is pressed, or when something activates it.</summary>
    OnInput,

    /// <summary>While its input is held: a release cancels it.</summary>
    WhileInputHeld,

    /// <summary>As soon as it is granted, for passive abilities.</summary>
    OnGranted,
}
