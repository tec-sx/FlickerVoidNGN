using System.Reflection;

using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Shaders;

namespace FlickerVoid.Engine.Rendering;

/// <summary>Compiles the shaders embedded in this assembly from Shaders/.</summary>
// TODO(Stage 5): load compiled bytecode from the asset pipeline instead of compiling at startup.
internal static class EmbeddedShaders
{
    public static ShaderBytecode Compile(IShaderCompiler compiler, string fileName, string entryPoint, ShaderStage stage)
    {
        string source = Read(fileName);
        ShaderSource shader = new(source, entryPoint, stage) { Name = fileName };
        ShaderCompileResult result = compiler.Compile(shader);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"{fileName} {entryPoint} failed to compile:\n{result.Messages}");
        }

        return new ShaderBytecode(result.Bytecode, entryPoint);
    }

    private static string Read(string fileName)
    {
        Assembly assembly = typeof(EmbeddedShaders).Assembly;
        using Stream stream = assembly.GetManifestResourceStream($"FlickerVoid.Engine.Rendering.Shaders.{fileName}")
            ?? throw new InvalidOperationException($"{fileName} isn't embedded in {assembly.GetName().Name}.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
