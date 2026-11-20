using FlickerVoid.Editor.Assets.Materials;
using FlickerVoid.Engine.Rendering.Materials;

namespace FlickerVoid.Editor.Materials;

/// <summary>Renders a material on a preview shape.</summary>
public interface IMaterialPreview
{
    /// <summary>A square image of the material on the shape turned by the yaw in radians, as Rgba8 pixels, top row first.</summary>
    byte[] Render(MaterialFile material, PreviewShape shape, float yaw, int size);
}
