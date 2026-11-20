namespace FlickerVoid.Engine.Gameplay.Conditions;

/// <summary>Which of the context's entities a condition or effect looks at.</summary>
public enum ContextEntity : byte
{
    /// <summary>The context's instigator, usually the player.</summary>
    Instigator,

    /// <summary>The context's target, usually the character the player deals with.</summary>
    Target,
}
