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
}
