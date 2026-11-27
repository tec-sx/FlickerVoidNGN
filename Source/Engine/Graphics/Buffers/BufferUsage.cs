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

    /// <summary>
    /// Acceleration structure builds read it: a mesh's vertices or indices, or a scene's instances. Ignored on devices without
    /// <see cref="Devices.GraphicsFeatures.RayQueries"/>.
    /// </summary>
    AccelerationStructureInput = 32,

    /// <summary>Indirect draws read their arguments from it, which shaders may have written.</summary>
    Indirect = 64,
}

