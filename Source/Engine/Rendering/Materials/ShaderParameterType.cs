namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>What a material parameter holds, which decides its size in the material's buffer.</summary>
public enum ShaderParameterType
{
    /// <summary>One float.</summary>
    Float,

    /// <summary>A linear RGB colour, three floats.</summary>
    Color,

    /// <summary>A texture named by its source file, packed as the index the shader samples it by.</summary>
    Texture,
}
