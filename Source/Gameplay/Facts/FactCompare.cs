namespace FlickerVoid.Engine.Gameplay.Facts;

/// <summary>Compare plus the existence checks only facts have.</summary>
public enum FactCompare : byte
{
    Equal,
    NotEqual,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual,
    Defined,
    Undefined,
}
