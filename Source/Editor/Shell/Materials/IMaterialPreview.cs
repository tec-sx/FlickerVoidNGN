using FlickerVoid.Engine.Rendering.Materials;

namespace FlickerVoid.Editor.Materials;

/// <summary>Renders a material on a preview shape.</summary>
public interface IMaterialPreview
{
    /// <summary>A square image of the material as the view shows it, as Rgba8 pixels, top row first.</summary>
    byte[] Render(MaterialFile material, PreviewView view, int size);
}
