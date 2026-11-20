namespace FlickerVoid.Engine.Graphics.Commands;

/// <summary>What a render pass does with a target's contents when it starts.</summary>
public enum LoadAction : byte
{
    Load,
    Clear,

    /// <summary>The pass overwrites every pixel, so the old contents don't matter.</summary>
    DontCare,
}

