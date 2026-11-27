using System.Numerics;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Buildings.Plans;

/// <summary>
/// How a flight of stairs lies, in its own space: its foot's middle at the origin, climbing toward +Z, the height between its floors
/// shared by even steps. Each step and the landing is a box from the floor up; the run under the floor above that a climber's head
/// would hit is opened, and a railing runs round the opening but for the side the stairs come up.
/// </summary>
public sealed class StairsLayout
{
    private const float RailHeight = 1;
    private const float RailThickness = 0.05f;
    private const float Gap = 0.1f;

    private StairsLayout()
    {
    }

    /// <summary>Each step's and the landing's footprint (low and high corners across the floor) with the height of its top.</summary>
    public List<(Vector2 Low, Vector2 High, float Top)> Blocks { get; } = [];

    /// <summary>The footprints to open in the floor above.</summary>
    public List<(Vector2 Low, Vector2 High)> Openings { get; } = [];

    /// <summary>The railing's runs on the floor above, each from one end to the other.</summary>
    public List<(Vector2 From, Vector2 To)> Rails { get; } = [];

    /// <summary>How many steps there are, the floor above among them when the stairs meet it.</summary>
    public int Steps { get; private set; }

    /// <summary>How much each step rises.</summary>
    public float StepRise { get; private set; }

    /// <summary>Lays out the stairs climbing <paramref name="height"/> to a floor <paramref name="upperFloor"/> thick.</summary>
    public static StairsLayout Create(BuildingStairs stairs, float height, float upperFloor)
    {
        StairsLayout layout = new();
        int steps = Math.Max(2, (int)MathF.Ceiling(height / Math.Max(stairs.Rise, 0.05f)));
        float rise = height / steps;
        int blocks = stairs.MeetsFloor ? steps - 1 : steps;
        layout.Steps = steps;
        layout.StepRise = rise;
        float tread = Math.Max(stairs.Tread, 0.1f);
        float half = stairs.Width / 2;
        float side = stairs.TurnLeft ? 1 : -1;
        float clearance = height - upperFloor - stairs.Headroom;
        float margin = stairs.Margin;

        if (stairs.Kind == StairsKind.Straight)
        {
            layout.AddRun(Vector2.Zero, Vector2.UnitY, half, 0, blocks, rise, tread, clearance, margin, true, out _);
            layout.AddRails();
            return layout;
        }

        int first = Math.Clamp((int)MathF.Round(stairs.FirstRun / tread), 1, Math.Max(1, blocks - 2));
        int second = Math.Max(0, blocks - first - 1);
        layout.AddRun(Vector2.Zero, Vector2.UnitY, half, 0, first, rise, tread, clearance, margin, false, out float end);
        float landingTop = (first + 1) * rise;
        float depth = Math.Max(stairs.Landing, stairs.Width);
        if (stairs.Kind == StairsKind.L)
        {
            // The landing is as deep as the stairs are wide; the second run leaves it sideways.
            Vector2 low = new(-half, end);
            Vector2 high = new(half, end + depth);
            layout.AddBlock(low, high, landingTop, clearance, margin);
            Vector2 start = new(side * half, end + (depth / 2));
            layout.AddRun(start, new Vector2(side, 0), stairs.Width / 2, first + 1, second, rise, tread, clearance, margin, true, out _);
        }
        else
        {
            float across = stairs.Width + Gap;
            float left = Math.Min(-half, -half + (side * across));
            float right = Math.Max(half, half + (side * across));
            layout.AddBlock(new Vector2(left, end), new Vector2(right, end + depth), landingTop, clearance, margin);
            Vector2 start = new(side * across, end);
            layout.AddRun(start, -Vector2.UnitY, half, first + 1, second, rise, tread, clearance, margin, true, out _);
        }

        layout.AddOpeningRails(margin);
        return layout;
    }

    /// <summary>The steps and landing as one solid of boxes, in the stairs' own space.</summary>
    public SolidMesh CreateSolid()
    {
        List<SolidMesh> boxes = [];
        foreach ((Vector2 low, Vector2 high, float top) in Blocks)
        {
            boxes.Add(Box(low, high, 0, top));
        }

        return OpeningShape.Join(boxes);
    }

    /// <summary>The railing as one solid of thin walls, in the stairs' own space, standing on the floor above at <paramref name="floor"/>.</summary>
    public SolidMesh? CreateRailing(float floor)
    {
        return CreateRails(Rails, floor);
    }

    /// <summary>Thin walls along the runs, standing on <paramref name="floor"/>; null for none.</summary>
    public static SolidMesh? CreateRails(IReadOnlyList<(Vector2 From, Vector2 To)> rails, float floor)
    {
        List<SolidMesh> walls = [];
        foreach ((Vector2 from, Vector2 to) in rails)
        {
            Vector2 run = to - from;
            float length = run.Length();
            if (length < 0.05f)
            {
                continue;
            }

            Vector2 side = Polygons.Left(run) * (RailThickness / 2);
            Vector2 extend = Vector2.Normalize(run) * (RailThickness / 2);
            List<Vector2> left = [from - extend + side, to + extend + side];
            List<Vector2> right = [from - extend - side, to + extend - side];
            walls.Add(PlanGeometry.Ribbon(left, right, floor, floor + RailHeight));
        }

        if (walls.Count == 0)
        {
            return null;
        }

        return OpeningShape.Join(walls);
    }

    /// <summary>A box between two corners across the floor, from <paramref name="bottom"/> to <paramref name="top"/>.</summary>
    public static SolidMesh Box(Vector2 low, Vector2 high, float bottom, float top)
    {
        Vector2 min = Vector2.Min(low, high);
        Vector2 max = Vector2.Max(low, high);
        List<Vector2> square = [min, new(max.X, min.Y), max, new(min.X, max.Y)];
        return Polygons.Prism(square, bottom, top);
    }

    /// <summary>
    /// Adds a run of steps, and the opening over those that rise into the floor above, wider by the margin all round but where the run
    /// <paramref name="arrives"/> at that floor, which meets its top step.
    /// </summary>
    private void AddRun(Vector2 start, Vector2 direction, float half, int firstStep, int count, float rise, float tread, float clearance, float margin, bool arrives, out float end)
    {
        Vector2 across = new(direction.Y, -direction.X);
        float firstOpen = -1;
        Vector2 openLow = Vector2.Zero;
        Vector2 openHigh = Vector2.Zero;
        for (int step = 0; step < count; step++)
        {
            Vector2 near = start + (direction * (step * tread));
            Vector2 far = start + (direction * ((step + 1) * tread));
            Vector2 low = Vector2.Min(Vector2.Min(near + (across * half), near - (across * half)), Vector2.Min(far + (across * half), far - (across * half)));
            Vector2 high = Vector2.Max(Vector2.Max(near + (across * half), near - (across * half)), Vector2.Max(far + (across * half), far - (across * half)));
            float top = (firstStep + step + 1) * rise;
            Blocks.Add((low, high, top));
            if (top > clearance)
            {
                if (firstOpen < 0)
                {
                    firstOpen = step;
                    openLow = low;
                    openHigh = high;
                }
                else
                {
                    openLow = Vector2.Min(openLow, low);
                    openHigh = Vector2.Max(openHigh, high);
                }
            }
        }

        if (firstOpen >= 0)
        {
            Vector2 low = openLow - new Vector2(margin);
            Vector2 high = openHigh + new Vector2(margin);
            if (arrives)
            {
                (low, high) = KeepArrival(low, high, openLow, openHigh, direction);
            }

            Openings.Add((low, high));
        }

        end = (start + (direction * (count * tread))).Y;
    }

    /// <summary>The opening with its side in the run's direction drawn back to the steps' own edge.</summary>
    private static (Vector2 Low, Vector2 High) KeepArrival(Vector2 low, Vector2 high, Vector2 stepsLow, Vector2 stepsHigh, Vector2 direction)
    {
        if (direction.X > 0.5f)
        {
            high.X = stepsHigh.X;
        }
        else if (direction.X < -0.5f)
        {
            low.X = stepsLow.X;
        }
        else if (direction.Y > 0.5f)
        {
            high.Y = stepsHigh.Y;
        }
        else if (direction.Y < -0.5f)
        {
            low.Y = stepsLow.Y;
        }

        return (low, high);
    }

    private void AddBlock(Vector2 low, Vector2 high, float top, float clearance, float margin)
    {
        Blocks.Add((low, high, top));
        if (top > clearance)
        {
            Openings.Add((low - new Vector2(margin), high + new Vector2(margin)));
        }
    }

    /// <summary>Straight stairs: a rail down each side of the opening and across its foot, none where they arrive.</summary>
    private void AddRails()
    {
        if (Openings.Count == 0)
        {
            return;
        }

        (Vector2 low, Vector2 high) = Openings[0];
        Rails.Add((new Vector2(low.X, low.Y), new Vector2(low.X, high.Y)));
        Rails.Add((new Vector2(high.X, low.Y), new Vector2(high.X, high.Y)));
        Rails.Add((new Vector2(low.X, low.Y), new Vector2(high.X, low.Y)));
    }

    /// <summary>
    /// A turning flight: a rail along each edge of the openings' outline that no other opening touches, but for the edge where the
    /// last run arrives (the far end of the last opening).
    /// </summary>
    private void AddOpeningRails(float margin)
    {
        if (Openings.Count == 0)
        {
            return;
        }

        int last = Openings.Count - 1;
        for (int index = 0; index < Openings.Count; index++)
        {
            (Vector2 low, Vector2 high) = Openings[index];
            (Vector2, Vector2)[] edges =
            [
                (new Vector2(low.X, low.Y), new Vector2(high.X, low.Y)),
                (new Vector2(high.X, low.Y), new Vector2(high.X, high.Y)),
                (new Vector2(high.X, high.Y), new Vector2(low.X, high.Y)),
                (new Vector2(low.X, high.Y), new Vector2(low.X, low.Y)),
            ];
            foreach ((Vector2 from, Vector2 to) in edges)
            {
                Vector2 middle = (from + to) / 2;
                if (index == last && IsArrival(middle, low, high))
                {
                    continue;
                }

                if (IsInsideAnother(middle, index, margin))
                {
                    continue;
                }

                Rails.Add((from, to));
            }
        }
    }

    // The last run's far end: the edge of its opening the last step's block reaches.
    private bool IsArrival(Vector2 middle, Vector2 low, Vector2 high)
    {
        (Vector2 stepLow, Vector2 stepHigh, _) = Blocks[^1];
        Vector2 stepMiddle = (stepLow + stepHigh) / 2;
        Vector2 openMiddle = (low + high) / 2;
        Vector2 toStep = stepMiddle - openMiddle;
        Vector2 toEdge = middle - openMiddle;
        return Vector2.Dot(Vector2.Normalize(toStep), Vector2.Normalize(toEdge)) > 0.7f;
    }

    private bool IsInsideAnother(Vector2 point, int own, float margin)
    {
        for (int index = 0; index < Openings.Count; index++)
        {
            if (index == own)
            {
                continue;
            }

            (Vector2 low, Vector2 high) = Openings[index];
            if (point.X >= low.X - margin && point.X <= high.X + margin && point.Y >= low.Y - margin && point.Y <= high.Y + margin)
            {
                return true;
            }
        }

        return false;
    }
}
