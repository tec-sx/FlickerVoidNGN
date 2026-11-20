using System.Text.Json;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// A value a shader lets materials set, declared in its <c>Name.fvshader</c>: a float is written as a number, a colour as
/// an array of three; the optional limits are for editors.
/// </summary>
public sealed record ShaderParameter(string Name, ShaderParameterType Type, JsonElement Default)
{
    public float? Minimum { get; init; }

    public float? Maximum { get; init; }

    /// <summary>How many floats the parameter takes in the material's buffer.</summary>
    public int Components
    {
        get
        {
            if (Type == ShaderParameterType.Color)
            {
                return 3;
            }

            return 1;
        }
    }
}
