using FlickerVoid.Engine.Graphics.Resources;

namespace FlickerVoid.Engine.Graphics.AccelerationStructures;

/// <summary>
/// What rays are traced against: a bottom-level structure holds a mesh's triangles, a top-level one places bottom-level ones in a
/// scene and is what shaders trace against, through its bindless index; bottom-level structures have none.
/// </summary>
public interface IAccelerationStructure : IGraphicsResource
{
    /// <summary>Where the GPU finds it, which top-level instances name a bottom-level structure by.</summary>
    ulong Address { get; }
}
