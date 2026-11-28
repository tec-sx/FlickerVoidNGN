using System.Collections.ObjectModel;
using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>
/// One timeline's state, which every control of the view shares: the shown range, the time scale, the lanes and their items, the
/// regions over them, the cursor, the playhead and the selection. Edits go to the owning <see cref="ITimelineEditor"/>; after
/// changing items or regions the owner calls <see cref="Invalidate"/> so the controls draw again.
/// </summary>
public sealed partial class TimelineViewModel : ObservableObject
{
    public TimelineViewModel(ITimeScale scale, TimeRange? range = null)
    {
        Scale = scale;
        Range = range ?? new TimeRange();
        Range.PropertyChanged += HandleRangeChanged;
    }

    public TimeRange Range { get; }

    [ObservableProperty]
    public partial ITimeScale Scale { get; set; }

    public ObservableCollection<TimelineLane> Lanes { get; } = [];

    public List<TimelineRegion> Regions { get; } = [];

    /// <summary>Where editing happens and playing starts, kept within the scale's period or after 0.</summary>
    [ObservableProperty]
    public partial double Cursor { get; set; }

    /// <summary>Where playing is now, or NaN when nothing plays.</summary>
    [ObservableProperty]
    public partial double Playhead { get; set; } = double.NaN;

    /// <summary>Whether drags snap to the scale's step; holding Alt turns it off for one drag.</summary>
    [ObservableProperty]
    public partial bool Snaps { get; set; } = true;

    /// <summary>Whether dragging along the ruler's empty space draws a new region.</summary>
    public bool CanCreateRegions { get; set; }

    /// <summary>Whether regions may cover the same time; when not, drags stop at the neighbours.</summary>
    public bool RegionsOverlap { get; set; }

    public ITimelineEditor? Editor { get; set; }

    /// <summary>The pixels <see cref="Fit"/> leaves free at both ends, so a key at the start or end shows whole.</summary>
    public double FitInset { get; set; }

    /// <summary>Whether a resize fits everything again, for a timeline that always shows its whole length.</summary>
    public bool KeepsFit { get; set; }

    /// <summary>The region being drawn on the ruler, before it is made, or null.</summary>
    public TimelineRegion? Draft { get; set; }

    /// <summary>Counts the changes of items and regions, so the controls draw again when it moves.</summary>
    [ObservableProperty]
    public partial int Revision { get; private set; }

    /// <summary>The latest end of any item or region, or 0.</summary>
    public double End
    {
        get
        {
            double end = 0;
            foreach (TimelineRow row in GetRows())
            {
                foreach (TimelineItem item in row.Lane.Items)
                {
                    end = Math.Max(end, item.End);
                }
            }

            foreach (TimelineRegion region in Regions)
            {
                end = Math.Max(end, region.End);
            }

            return end;
        }
    }

    public double ContentHeight
    {
        get
        {
            IReadOnlyList<TimelineRow> rows = GetRows();
            if (rows.Count == 0)
            {
                return 0;
            }

            return rows[^1].Bottom;
        }
    }

    public IEnumerable<TimelineItem> SelectedItems
    {
        get
        {
            foreach (TimelineRow row in GetRows())
            {
                foreach (TimelineItem item in row.Lane.Items)
                {
                    if (item.IsSelected)
                    {
                        yield return item;
                    }
                }
            }
        }
    }

    public TimelineRegion? SelectedRegion => Regions.FirstOrDefault(region => region.IsSelected);

    /// <summary>Raised when the selected items or region change.</summary>
    public event Action? SelectionChanged;

    public void Invalidate() => Revision++;

    /// <summary>The lanes laid out from the top, each followed by the lanes under it.</summary>
    public IReadOnlyList<TimelineRow> GetRows()
    {
        List<TimelineRow> rows = [];
        double top = 0;
        foreach (TimelineLane lane in Lanes)
        {
            top = AddRows(rows, lane, top, 0);
        }

        return rows;
    }

    /// <summary>Selects an item alone, or adds it to the selection (taking it out when it is in), or clears the selection with null.</summary>
    public void Select(TimelineItem? item, bool add = false)
    {
        if (add && item is not null)
        {
            item.IsSelected = !item.IsSelected;
            Invalidate();
            SelectionChanged?.Invoke();
            return;
        }

        ClearSelection();
        item?.IsSelected = true;
        Invalidate();
        SelectionChanged?.Invoke();
    }

    public void SelectRegion(TimelineRegion? region)
    {
        ClearSelection();
        region?.IsSelected = true;
        Invalidate();
        SelectionChanged?.Invoke();
    }

    /// <summary>Moves the cursor, wrapped into the scale's period when it repeats, else kept after 0.</summary>
    public void MoveCursor(double time)
    {
        double wrapped = Scale.Wrap(time);
        if (Scale.Period is null)
        {
            wrapped = Math.Max(wrapped, 0);
        }

        Cursor = wrapped;
    }

    /// <summary>Shows everything from 0 to the end (or one period), with <see cref="FitInset"/> left at both ends.</summary>
    public void Fit()
    {
        double end = End;
        if (Scale.Period is { } period)
        {
            end = Math.Max(end, period);
        }

        Range.Frame(0, Math.Max(end, 1), inset: FitInset);
    }

    private void HandleRangeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (KeepsFit && e.PropertyName == nameof(TimeRange.Width))
        {
            Fit();
        }
    }

    private void ClearSelection()
    {
        foreach (TimelineRow row in GetRows())
        {
            foreach (TimelineItem item in row.Lane.Items)
            {
                item.IsSelected = false;
            }
        }

        foreach (TimelineRegion region in Regions)
        {
            region.IsSelected = false;
        }
    }

    private static double AddRows(List<TimelineRow> rows, TimelineLane lane, double top, int depth)
    {
        rows.Add(new TimelineRow(lane, top, depth));
        double next = top + lane.Height;
        foreach (TimelineLane child in lane.Children)
        {
            next = AddRows(rows, child, next, depth + 1);
        }

        return next;
    }
}
