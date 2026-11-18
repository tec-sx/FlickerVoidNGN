namespace FlickerVoid.Engine.Shaders;

/// <summary>The bytecode a shader compiles to: DXIL for Direct3D 12, SPIR-V for Vulkan.</summary>
public enum ShaderTarget : byte
{
    Dxil,
    Spirv,
}
