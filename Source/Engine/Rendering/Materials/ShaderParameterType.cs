namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>What a material parameter holds, which decides its size in the material's buffer.</summary>
public enum ShaderParameterType
{
    /// <summary>One float.</summary>
    Float,

    /// <summary>A linear RGB colour, three floats.</summary>
    Color,
}
