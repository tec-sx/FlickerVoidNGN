using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>The painters of timeline items by kind; the kit's own are registered from the start, and an editor adds its kinds.</summary>
public static class TimelinePainters
{
    private static readonly Dictionary<string, ILaneItemPainter> Painters = new()
    {
        [TimelineKinds.Clip] = new ClipPainter(),
        [TimelineKinds.Fill] = new FillPainter(),
        [TimelineKinds.Key] = new KeyPainter(),
        [TimelineKinds.Marker] = new MarkerPainter(),
    };

    public static void Register(string kind, ILaneItemPainter painter) => Painters[kind] = painter;

    /// <summary>The painter of a kind, or the clip painter for a kind nobody registered.</summary>
    public static ILaneItemPainter Find(string kind)
    {
        if (Painters.TryGetValue(kind, out ILaneItemPainter? painter))
        {
            return painter;
        }

        return Painters[TimelineKinds.Clip];
    }
}
