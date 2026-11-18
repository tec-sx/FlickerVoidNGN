namespace FlickerVoid.Engine.Gameplay.Conditions;

/// <summary>How the player sees what a condition set guards while the set fails.</summary>
public enum ConditionFailurePresentation : byte
{
    Hidden,
    ShowLocked,
    ShowLockedWithReason,
}
