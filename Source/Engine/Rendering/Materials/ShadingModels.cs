namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>The parameters of a master each shading model reads in the lighting pass, packed after the scene shader's.</summary>
public static class ShadingModels
{
    /// <summary>
    /// The names of the parameters whose values the model reads, in the order they fill the four model values; a master declares
    /// them among its own parameters, as floats or colors, and one it leaves out reads 0.
    /// </summary>
    public static IReadOnlyList<string> GetValues(ShadingModel model)
    {
        switch (model)
        {
            case ShadingModel.Metal:
                return ["metalness"];
            case ShadingModel.Glow:
                return ["glow", "lit"];
            case ShadingModel.Skin:
                return ["scatter", "scatterWidth"];
            default:
                return [];
        }
    }
}
