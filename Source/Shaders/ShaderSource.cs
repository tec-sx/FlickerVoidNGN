namespace FlickerVoid.Engine.Shaders;

/// <summary>HLSL source and the entry point to compile from it.</summary>
public sealed record ShaderSource(string Code, string EntryPoint, ShaderStage Stage)
{
    /// <summary>Shown in compiler messages.</summary>
    public string Name { get; init; } = "shader.hlsl";

    /// <summary>Preprocessor defines, as NAME or NAME=VALUE.</summary>
    public IReadOnlyList<string> Defines { get; init; } = [];
}
