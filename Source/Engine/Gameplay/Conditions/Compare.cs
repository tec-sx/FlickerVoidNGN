namespace FlickerVoid.Engine.Gameplay.Conditions;

/// <summary>How a current value is compared with a target value.</summary>
public enum Compare : byte
{
    Equal,
    NotEqual,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual,
}
