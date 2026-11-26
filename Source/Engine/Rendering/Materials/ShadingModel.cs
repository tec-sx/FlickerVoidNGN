namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// How the lighting pass lights a master's surfaces; the shaders' <c>ShadingModel</c> values follow this order. A model reads
/// up to four values of the material's (<see cref="ShadingModels.GetValues"/>) beside the scene shader's controls.
/// </summary>
public enum ShadingModel
{
    /// <summary>The painted look every material had before masters: ramp, wrap, shadow tint, rim, highlight and glow.</summary>
    Painted,

    /// <summary>Paint over metal: the more metal, the less diffuse light and the more the highlight and reflection take the paint's color (<c>metalness</c>).</summary>
    Metal,

    /// <summary>Paint that shines by itself, such as a neon tube or a screen: its color times <c>glow</c>, plus the light it takes scaled by <c>lit</c>.</summary>
    Glow,

    /// <summary>Skin, wax or leaves: light wraps further round the terminator in the <c>scatter</c> color, by <c>scatterWidth</c>, as it scatters under the surface.</summary>
    Skin,

    /// <summary>
    /// Glass, drawn after lighting over the lit scene and never into the G-buffer: what is behind it bent by <c>bend</c> and blurred
    /// by <c>frost</c>, tinted by its color, with a reflection toward its edges and its highlights; where its paint covers it, the
    /// paint is lit as paint is.
    /// </summary>
    Glass,
}
