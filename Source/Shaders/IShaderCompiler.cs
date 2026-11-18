namespace FlickerVoid.Engine.Shaders;

/// <summary>Compiles HLSL to SPIR-V, the bytecode Vulkan loads.</summary>
public interface IShaderCompiler
{
    ShaderCompileResult Compile(ShaderSource source);
}
