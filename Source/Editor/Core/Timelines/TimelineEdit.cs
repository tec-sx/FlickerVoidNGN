namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>
/// The pointer and keyboard logic of every timeline, as pure functions over its view model: what is under a point, and the drags,
/// nudges and deletes that follow, snapped and clamped, handed to the owning editor as <see cref="TimelineChange"/>s.
/// </summary>
public static class TimelineEdit
{
    /// <summary>How close, in pixels, the pointer must come to a span's edge to resize it.</summary>
    public const double EdgePixels = 6;

    /// <summary>How close, in pixels, the pointer must come to a point to pick it.</summary>
    public const double PointPixels = 7;

    /// <summary>How far, in pixels, a press on the ruler must move before it draws a region instead of scrubbing.</summary>
    public const double DragPixels = 4;

    /// <summary>What is under a point of the lanes, in pixels from their top left.</summary>
    public static TimelineHit HitLanes(TimelineViewModel timeline, double x, double y)
    {
        foreach (TimelineRow row in timeline.GetRows())
        {
            if (y < row.Top || y >= row.Bottom)
            {
                continue;
            }

            List<TimelineItem> items = row.Lane.Items;
            for (int index = items.Count - 1; index >= 0; index--)
            {
                TimelineHitKind kind = HitItem(timeline.Range, items[index], x);
                if (kind != TimelineHitKind.None)
                {
                    return new TimelineHit(kind, row, items[index], null);
                }
            }

            return new TimelineHit(TimelineHitKind.Lane, row, null, null);
        }

        return TimelineHit.None;
    }

    /// <summary>What is under a point of the ruler: a region's tab or edge, or the ruler itself.</summary>
    public static TimelineHit HitRuler(TimelineViewModel timeline, double x)
    {
        for (int index = timeline.Regions.Count - 1; index >= 0; index--)
        {
            TimelineRegion region = timeline.Regions[index];
            TimelineHitKind kind = HitSpan(timeline.Range, region.Start, region.End, x, canResize: true);
            if (kind == TimelineHitKind.None)
            {
                continue;
            }

            TimelineHitKind regionKind = kind switch
            {
                TimelineHitKind.ItemStart => TimelineHitKind.RegionStart,
                TimelineHitKind.ItemEnd => TimelineHitKind.RegionEnd,
                _ => TimelineHitKind.RegionTab,
            };
            return new TimelineHit(regionKind, null, null, region);
        }

        return new TimelineHit(TimelineHitKind.Ruler, null, null, null);
    }

    /// <summary>Starts a drag where the pointer pressed: selects what it hit and moves the cursor for the ruler or an empty lane.</summary>
    public static TimelineDrag Press(TimelineViewModel timeline, TimelineHit hit, double x, bool snap, bool add)
    {
        TimelineDragMode mode = TimelineDragMode.None;
        switch (hit.Kind)
        {
            case TimelineHitKind.Item:
            case TimelineHitKind.ItemStart:
            case TimelineHitKind.ItemEnd:
                timeline.Select(hit.Item, add);
                mode = GetItemMode(hit, add);
                break;
            case TimelineHitKind.RegionTab:
                timeline.SelectRegion(hit.Region);
                mode = TimelineDragMode.MoveRegion;
                break;
            case TimelineHitKind.RegionStart:
                timeline.SelectRegion(hit.Region);
                mode = TimelineDragMode.ResizeRegionStart;
                break;
            case TimelineHitKind.RegionEnd:
                timeline.SelectRegion(hit.Region);
                mode = TimelineDragMode.ResizeRegionEnd;
                break;
            case TimelineHitKind.Lane:
                timeline.Select(null);
                mode = TimelineDragMode.Scrub;
                break;
            case TimelineHitKind.Ruler:
                mode = TimelineDragMode.RulerPress;
                break;
        }

        TimelineDrag drag = new(timeline, mode, hit, x);
        if (mode is TimelineDragMode.Scrub or TimelineDragMode.RulerPress)
        {
            drag.Move(x, snap);
        }

        return drag;
    }

    /// <summary>Moves the selected items by whole snap steps, later for positive steps.</summary>
    public static void Nudge(TimelineViewModel timeline, int steps)
    {
        if (timeline.Editor is not { } editor)
        {
            return;
        }

        double step = timeline.Scale.GetSnapStep(timeline.Range);
        bool changed = false;
        List<TimelineItem> selected = timeline.SelectedItems.ToList();
        foreach (TimelineItem item in selected)
        {
            if (!item.CanMove)
            {
                continue;
            }

            double start = ClampStart(timeline, item.Start + (steps * step), item.Length);
            editor.Apply(new TimelineChange(TimelineChangeKind.MoveItem, item, null, start, item.Length));
            changed = true;
        }

        if (changed)
        {
            editor.Complete();
        }
    }

    /// <summary>Asks the owner to delete the selected items and region, as one undo step.</summary>
    public static void DeleteSelected(TimelineViewModel timeline)
    {
        if (timeline.Editor is not { } editor)
        {
            return;
        }

        List<TimelineItem> selected = timeline.SelectedItems.ToList();
        foreach (TimelineItem item in selected)
        {
            editor.Apply(new TimelineChange(TimelineChangeKind.DeleteItem, item, null, item.Start, item.Length));
        }

        TimelineRegion? region = timeline.SelectedRegion;
        if (region is not null)
        {
            editor.Apply(new TimelineChange(TimelineChangeKind.DeleteRegion, null, region, region.Start, region.Length));
        }

        if (selected.Count > 0 || region is not null)
        {
            editor.Complete();
        }
    }

    /// <summary>A time snapped to the scale's step when snapping is on for the timeline and for this drag.</summary>
    public static double Snap(TimelineViewModel timeline, double time, bool snap)
    {
        if (!snap || !timeline.Snaps)
        {
            return time;
        }

        return timeline.Scale.Snap(time, timeline.Range);
    }

    /// <summary>The shortest a span may be: one snap step, or one pixel's time when not snapping.</summary>
    public static double GetMinLength(TimelineViewModel timeline, bool snap)
    {
        if (!snap || !timeline.Snaps)
        {
            return 1 / timeline.Range.PixelsPerUnit;
        }

        return timeline.Scale.GetSnapStep(timeline.Range);
    }

    /// <summary>A start kept after 0, and within one period with the length when the scale repeats.</summary>
    public static double ClampStart(TimelineViewModel timeline, double start, double length)
    {
        double clamped = Math.Max(start, 0);
        if (timeline.Scale.Period is { } period)
        {
            clamped = Math.Min(clamped, Math.Max(period - length, 0));
        }

        return clamped;
    }

    private static TimelineDragMode GetItemMode(TimelineHit hit, bool add)
    {
        if (add || hit.Item is not { } item)
        {
            return TimelineDragMode.None;
        }

        if (hit.Kind == TimelineHitKind.ItemStart && item.CanResize)
        {
            return TimelineDragMode.ResizeItemStart;
        }

        if (hit.Kind == TimelineHitKind.ItemEnd && item.CanResize)
        {
            return TimelineDragMode.ResizeItemEnd;
        }

        if (item.CanMove)
        {
            return TimelineDragMode.MoveItem;
        }

        return TimelineDragMode.None;
    }

    private static TimelineHitKind HitItem(TimeRange range, TimelineItem item, double x)
    {
        if (!item.IsPoint)
        {
            return HitSpan(range, item.Start, item.End, x, item.CanResize);
        }

        double at = range.ToX(item.Start);
        if (Math.Abs(x - at) <= PointPixels)
        {
            return TimelineHitKind.Item;
        }

        return TimelineHitKind.None;
    }

    /// <summary>Hits a span's body, or its edges when it resizes; on a narrow span the edges take at most a third each.</summary>
    private static TimelineHitKind HitSpan(TimeRange range, double start, double end, double x, bool canResize)
    {
        double left = range.ToX(start);
        double right = range.ToX(end);
        double edge = Math.Min(EdgePixels, (right - left) / 3);
        if (canResize && Math.Abs(x - left) <= edge)
        {
            return TimelineHitKind.ItemStart;
        }

        if (canResize && Math.Abs(x - right) <= edge)
        {
            return TimelineHitKind.ItemEnd;
        }

        if (x >= left && x <= right)
        {
            return TimelineHitKind.Item;
        }

        return TimelineHitKind.None;
    }
}
