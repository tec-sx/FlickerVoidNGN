namespace FlickerVoid.Engine.Graphics.Pipelines;

/// <summary>How a depth test compares the new value with the stored one.</summary>
public enum CompareFunction : byte
{
    Never,
    Less,
    Equal,
    LessEqual,
    Greater,
    NotEqual,
    GreaterEqual,
    Always,
}

