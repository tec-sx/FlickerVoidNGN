namespace FlickerVoid.Gameplay.Protagonist.Identity.Social.Affiliation;

/// <summary>How members of a faction treat someone, from worst to best.</summary>
public enum Attitude : byte
{
    /// <summary>Attacks or reports on sight.</summary>
    Hostile,

    /// <summary>Refuses help and raises prices.</summary>
    Unfriendly,

    /// <summary>No opinion either way.</summary>
    Neutral,

    /// <summary>Helps and gives better prices.</summary>
    Friendly,

    /// <summary>Treats the subject as one of their own.</summary>
    Allied,
}
