namespace FlickerVoid.Engine.Graphics.Buffers;

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

    /// <summary>Shaders write it as a RWByteAddressBuffer or RWStructuredBuffer through its bindless index.</summary>
    ShaderWrite = 16,
}

