using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Cameras.Triggers;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>Finds the music areas of a scene and where their name chips hang in a viewport.</summary>
public static class MusicAreaLabels
{
    private static readonly QueryDescription Areas = new QueryDescription().WithAll<MusicArea, TriggerArea, Transform>();

    public static IReadOnlyList<MusicAreaLabel> Collect(World world, EditorCamera camera, int width, int height)
    {
        List<MusicAreaLabel> labels = [];
        world.Query(in Areas, (Entity entity, ref MusicArea music, ref TriggerArea area, ref Transform transform) =>
        {
            string name = world.TryGet(entity, out EntityName named) ? named.Value : "Music area";
            Matrix4x4 matrix = transform.ToMatrix();
            Vector3 top = area.Offset + new Vector3(0, area.Size.Y / 2, 0);
            Vector3 point = Vector3.Transform(top, matrix);
            Vector2 pixel = camera.Project(point, width, height);
            if (pixel.X >= 0 && pixel.X <= width && pixel.Y >= 0 && pixel.Y <= height)
            {
                labels.Add(new MusicAreaLabel(name, string.Join(", ", music.Layers), pixel.X, pixel.Y));
            }
        });
        return labels;
    }
}
