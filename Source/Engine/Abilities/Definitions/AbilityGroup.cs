namespace FlickerVoid.Engine.Abilities.Definitions;

/// <summary>How an ability runs beside others; at most one exclusive ability runs at a time.</summary>
public enum AbilityGroup : byte
{
    /// <summary>Runs beside anything.</summary>
    Independent,

    /// <summary>Exclusive, and replaced when another exclusive ability starts.</summary>
    ExclusiveReplace,

    /// <summary>Exclusive, and no other exclusive ability starts while it runs.</summary>
    ExclusiveBlock,
}
