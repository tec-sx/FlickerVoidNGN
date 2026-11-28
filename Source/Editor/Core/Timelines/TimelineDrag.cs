namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>
/// One drag on a timeline, from the press to the release: it scrubs the cursor, draws a new region, or moves or resizes an item or
/// a region from where they were when it began, asking the owner for each step and completing once when released.
/// </summary>
public sealed class TimelineDrag
{
    /// <summary>How far, in pixels, the pointer must move before a move or resize changes anything.</summary>
    private const double StillPixels = 1;

    private readonly TimelineViewModel _timeline;
    private readonly double _pressX;
    private readonly double _start;
    private readonly double _length;
    private readonly TimelineItem? _item;
    private readonly TimelineRegion? _region;
    private double _anchor;
    private bool _changed;

    internal TimelineDrag(TimelineViewModel timeline, TimelineDragMode mode, TimelineHit hit, double x)
    {
        _timeline = timeline;
        Mode = mode;
        _pressX = x;
        _item = hit.Item;
        _region = hit.Region;
        if (_item is not null)
        {
            _start = _item.Start;
            _length = _item.Length;
        }

        if (_region is not null)
        {
            _start = _region.Start;
            _length = _region.Length;
        }
    }

    public TimelineDragMode Mode { get; private set; }

    public void Move(double x, bool snap)
    {
        if (Mode is TimelineDragMode.Scrub)
        {
            Scrub(x, snap);
            return;
        }

        if (Mode is TimelineDragMode.RulerPress)
        {
            if (!_timeline.CanCreateRegions || Math.Abs(x - _pressX) <= TimelineEdit.DragPixels)
            {
                Scrub(x, snap);
                return;
            }

            Mode = TimelineDragMode.CreateRegion;
            double pressTime = _timeline.Range.ToTime(_pressX);
            _anchor = Math.Max(TimelineEdit.Snap(_timeline, pressTime, snap), 0);
        }

        if (Mode is TimelineDragMode.CreateRegion)
        {
            DrawRegion(x, snap);
            return;
        }

        if (Mode is TimelineDragMode.None || Math.Abs(x - _pressX) < StillPixels)
        {
            return;
        }

        double delta = (x - _pressX) / _timeline.Range.PixelsPerUnit;
        switch (Mode)
        {
            case TimelineDragMode.MoveItem:
                MoveItem(delta, snap);
                break;
            case TimelineDragMode.ResizeItemStart:
                ResizeItemStart(delta, snap);
                break;
            case TimelineDragMode.ResizeItemEnd:
                ResizeItemEnd(delta, snap);
                break;
            case TimelineDragMode.MoveRegion:
                MoveRegion(delta, snap);
                break;
            case TimelineDragMode.ResizeRegionStart:
                ResizeRegionStart(delta, snap);
                break;
            case TimelineDragMode.ResizeRegionEnd:
                ResizeRegionEnd(delta, snap);
                break;
        }
    }

    /// <summary>Ends the drag: makes the drawn region, and completes the owner's edits as one undo step.</summary>
    public void Release()
    {
        if (Mode is TimelineDragMode.CreateRegion && _timeline.Draft is { } draft)
        {
            _timeline.Draft = null;
            _timeline.Invalidate();
            Apply(new TimelineChange(TimelineChangeKind.CreateRegion, null, null, draft.Start, draft.Length));
        }

        if (_changed)
        {
            _timeline.Editor?.Complete();
        }

        Mode = TimelineDragMode.None;
    }

    private void Scrub(double x, bool snap)
    {
        double time = _timeline.Range.ToTime(x);
        double snapped = TimelineEdit.Snap(_timeline, time, snap);
        _timeline.MoveCursor(snapped);
    }

    private void DrawRegion(double x, bool snap)
    {
        double pointed = _timeline.Range.ToTime(x);
        double time = TimelineEdit.Snap(_timeline, pointed, snap);
        double start = Math.Max(Math.Min(_anchor, time), 0);
        double end = Math.Max(_anchor, time);
        if (!_timeline.RegionsOverlap)
        {
            (double earliest, double latest) = GetRoom(_anchor, _anchor, null);
            start = Math.Max(start, earliest);
            end = Math.Min(end, latest);
        }

        double minLength = TimelineEdit.GetMinLength(_timeline, snap);
        if (end - start < minLength)
        {
            end = start + minLength;
        }

        _timeline.Draft ??= new TimelineRegion { Name = "" };
        _timeline.Draft.Start = start;
        _timeline.Draft.Length = end - start;
        _timeline.Invalidate();
    }

    private void MoveItem(double delta, bool snap)
    {
        double start = TimelineEdit.Snap(_timeline, _start + delta, snap);
        start = TimelineEdit.ClampStart(_timeline, start, _length);
        Apply(new TimelineChange(TimelineChangeKind.MoveItem, _item, null, start, _length));
    }

    private void ResizeItemStart(double delta, bool snap)
    {
        double end = _start + _length;
        double minLength = TimelineEdit.GetMinLength(_timeline, snap);
        double start = TimelineEdit.Snap(_timeline, _start + delta, snap);
        start = Math.Clamp(start, 0, Math.Max(end - minLength, 0));
        Apply(new TimelineChange(TimelineChangeKind.ResizeItem, _item, null, start, end - start));
    }

    private void ResizeItemEnd(double delta, bool snap)
    {
        double minLength = TimelineEdit.GetMinLength(_timeline, snap);
        double end = TimelineEdit.Snap(_timeline, _start + _length + delta, snap);
        double length = Math.Max(end - _start, minLength);
        Apply(new TimelineChange(TimelineChangeKind.ResizeItem, _item, null, _start, length));
    }

    private void MoveRegion(double delta, bool snap)
    {
        double start = TimelineEdit.Snap(_timeline, _start + delta, snap);
        start = TimelineEdit.ClampStart(_timeline, start, _length);
        if (!_timeline.RegionsOverlap)
        {
            (double earliest, double latest) = GetRoom(_start, _start + _length, _region);
            start = Math.Clamp(start, earliest, Math.Max(latest - _length, earliest));
        }

        Apply(new TimelineChange(TimelineChangeKind.MoveRegion, null, _region, start, _length));
    }

    private void ResizeRegionStart(double delta, bool snap)
    {
        double end = _start + _length;
        double minLength = TimelineEdit.GetMinLength(_timeline, snap);
        double start = TimelineEdit.Snap(_timeline, _start + delta, snap);
        double earliest = 0;
        if (!_timeline.RegionsOverlap)
        {
            earliest = GetRoom(_start, end, _region).Earliest;
        }

        start = Math.Clamp(start, earliest, Math.Max(end - minLength, earliest));
        Apply(new TimelineChange(TimelineChangeKind.ResizeRegion, null, _region, start, end - start));
    }

    private void ResizeRegionEnd(double delta, bool snap)
    {
        double minLength = TimelineEdit.GetMinLength(_timeline, snap);
        double end = TimelineEdit.Snap(_timeline, _start + _length + delta, snap);
        end = Math.Max(end, _start + minLength);
        if (!_timeline.RegionsOverlap)
        {
            double latest = GetRoom(_start, _start + _length, _region).Latest;
            end = Math.Min(end, Math.Max(latest, _start + minLength));
        }

        Apply(new TimelineChange(TimelineChangeKind.ResizeRegion, null, _region, _start, end - _start));
    }

    /// <summary>The free time around a span: the end of the region before it and the start of the region after it.</summary>
    private (double Earliest, double Latest) GetRoom(double start, double end, TimelineRegion? self)
    {
        double earliest = 0;
        double latest = double.PositiveInfinity;
        foreach (TimelineRegion other in _timeline.Regions)
        {
            if (other == self)
            {
                continue;
            }

            if (other.End <= start + 1e-9)
            {
                earliest = Math.Max(earliest, other.End);
            }
            else if (other.Start >= end - 1e-9)
            {
                latest = Math.Min(latest, other.Start);
            }
        }

        return (earliest, latest);
    }

    private void Apply(TimelineChange change)
    {
        if (_timeline.Editor is not { } editor)
        {
            return;
        }

        editor.Apply(change);
        _changed = true;
    }
}
