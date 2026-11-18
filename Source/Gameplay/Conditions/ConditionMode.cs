namespace FlickerVoid.Engine.Gameplay.Conditions;

/// <summary>Whether a condition set needs all of its conditions or any one of them.</summary>
public enum ConditionMode : byte
{
    All,
    Any,
}
