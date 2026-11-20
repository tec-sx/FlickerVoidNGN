namespace FlickerVoid.Tools.ShaderCompiler;

/// <summary>Compiled bytecode, empty when compiling failed, and the compiler's errors and warnings.</summary>
public sealed record ShaderCompileResult(bool Succeeded, byte[] Bytecode, string Messages);
