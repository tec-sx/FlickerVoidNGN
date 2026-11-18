namespace FlickerVoid.Engine.Shaders;

/// <summary>Compiles HLSL to the bytecode a graphics API loads.</summary>
public interface IShaderCompiler
{
    ShaderCompileResult Compile(ShaderSource source, ShaderTarget target);
}
