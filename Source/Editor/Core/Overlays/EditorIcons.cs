using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Overlays;

/// <summary>
/// The icons that show and pick entities with nothing else to see: lights (in their colour), cameras and irradiance volumes. They
/// keep their size on screen and are drawn over everything, so a light inside a wall can still be clicked.
/// </summary>
public sealed class EditorIcons
{
    public const float SizePixels = 32;

    public static readonly Vector4 SelectedColor = new(1, 0.55f, 0.1f, 1);

    private static readonly Vector4 PlainColor = new(0.9f, 0.9f, 0.9f, 1);
    private static readonly QueryDescription PointLights = new QueryDescription().WithAll<PointLight, Transform>();
    private static readonly QueryDescription SpotLights = new QueryDescription().WithAll<SpotLight, Transform>();
    private static readonly QueryDescription Cameras = new QueryDescription().WithAll<Camera, Transform>();
    private static readonly QueryDescription Volumes = new QueryDescription().WithAll<IrradianceVolume, Transform>();

    private readonly List<IconInstance> _icons = [];

    public IReadOnlyList<IconInstance> Icons => _icons;

    /// <summary>Gathers this frame's icons from the world, the selected entity's in the selection colour.</summary>
    public void Collect(World world, Entity? selected)
    {
        _icons.Clear();
        world.Query(in PointLights, (Entity entity, ref PointLight light, ref Transform transform) =>
        {
            Vector4 color = new(light.Color, 1);
            Add(entity, transform.Position, EditorIcon.PointLight, color, selected);
        });
        world.Query(in SpotLights, (Entity entity, ref SpotLight light, ref Transform transform) =>
        {
            Vector4 color = new(light.Color, 1);
            Add(entity, transform.Position, EditorIcon.SpotLight, color, selected);
        });
        world.Query(in Cameras, (Entity entity, ref Transform transform) =>
        {
            Add(entity, transform.Position, EditorIcon.Camera, PlainColor, selected);
        });
        world.Query(in Volumes, (Entity entity, ref Transform transform) =>
        {
            Add(entity, transform.Position, EditorIcon.IrradianceVolume, PlainColor, selected);
        });
    }

    /// <summary>The entity whose icon covers the pixel, the nearest to the camera when several do.</summary>
    public Entity? HitTest(EditorCamera camera, Vector2 pixel, int width, int height)
    {
        Entity? nearest = null;
        float nearestDepth = float.PositiveInfinity;
        float half = SizePixels / 2;
        Transform eye = camera.ToTransform();
        foreach (IconInstance icon in _icons)
        {
            Vector2 centre = camera.Project(icon.Position, width, height);
            Vector2 offset = Vector2.Abs(pixel - centre);
            if (offset.X > half || offset.Y > half)
            {
                continue;
            }

            Vector3 toIcon = icon.Position - eye.Position;
            float depth = Vector3.Dot(toIcon, camera.Forward);
            if (depth > 0 && depth < nearestDepth)
            {
                nearest = icon.Entity;
                nearestDepth = depth;
            }
        }

        return nearest;
    }

    private void Add(Entity entity, Vector3 position, EditorIcon icon, Vector4 color, Entity? selected)
    {
        Vector4 shown = color;
        if (entity == selected)
        {
            shown = SelectedColor;
        }

        _icons.Add(new IconInstance(entity, position, icon, shown));
    }
}
