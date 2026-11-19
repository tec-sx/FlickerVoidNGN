namespace FlickerVoid.Engine.Shaders;

/// <summary>HLSL source and the entry point to compile from it.</summary>
public sealed record ShaderSource(string Code, string EntryPoint, ShaderStage Stage)
{
    /// <summary>Shown in compiler messages; quoted includes are looked up in this file's folder first.</summary>
    public string Name { get; init; } = "shader.hlsl";

    /// <summary>Folders searched for includes after the shader's own.</summary>
    public IReadOnlyList<string> IncludeDirectories { get; init; } = [];

    /// <summary>Preprocessor defines, as NAME or NAME=VALUE.</summary>
    public IReadOnlyList<string> Defines { get; init; } = [];
}
