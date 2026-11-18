namespace FlickerVoid.Engine.Graphics;

/// <summary>What a buffer is used for.</summary>
[Flags]
public enum BufferUsage : byte
{
    None = 0,

    /// <summary>Shaders read it as a ByteAddressBuffer or StructuredBuffer through its bindless index.</summary>
    ShaderRead = 1,
    Index = 2,
    CopySource = 4,
    CopyDestination = 8,
}
