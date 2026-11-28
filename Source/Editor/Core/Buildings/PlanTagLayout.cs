using System.Numerics;

namespace FlickerVoid.Editor.Core.Buildings;

/// <summary>Keeps the plan's tags from covering each other: a tag standing over one placed before it steps down below it.</summary>
public static class PlanTagLayout
{
    /// <summary>The room left between two tags, in the view's units.</summary>
    public const float Gap = 3;

    private const float Height = 22;
    private const float Padding = 18;
    private const float Spacing = 6;
    private const float TitleWidth = 6.8f;
    private const float DetailWidth = 6.1f;
    private const float RoomTitleWidth = 7.6f;
    private const float RoomHeight = 34;

    /// <summary>How big a tag shows, guessed from the length of its title and detail in the small font.</summary>
    public static Vector2 Measure(string title, string detail)
    {
        float width = Padding + (title.Length * TitleWidth);
        if (detail.Length > 0)
        {
            width += Spacing + (detail.Length * DetailWidth);
        }

        return new Vector2(width, Height);
    }

    /// <summary>How big a room's name with its detail under it shows.</summary>
    public static Vector2 MeasureRoom(string title, string detail)
    {
        float width = Math.Max(title.Length * RoomTitleWidth, detail.Length * DetailWidth);
        return new Vector2(width, RoomHeight);
    }

    /// <summary>
    /// Where each tag's centre goes: the tag of a drag and the rooms' names stay where they point, then cuts and the rest in their
    /// order, each below what it would cover.
    /// </summary>
    public static Vector2[] Spread(IReadOnlyList<PlanTag> tags)
    {
        Vector2[] placed = new Vector2[tags.Count];
        List<int> done = [];
        for (int rank = 0; rank <= 3; rank++)
        {
            for (int index = 0; index < tags.Count; index++)
            {
                if (GetRank(tags[index].Kind) != rank)
                {
                    continue;
                }

                placed[index] = rank <= 1 ? tags[index].Centre : Clear(tags, placed, done, index);
                done.Add(index);
            }
        }

        return placed;
    }

    private static int GetRank(PlanLabelKind kind)
    {
        return kind switch
        {
            PlanLabelKind.Drag => 0,
            PlanLabelKind.Room => 1,
            PlanLabelKind.Cut => 2,
            _ => 3,
        };
    }

    private static Vector2 Clear(IReadOnlyList<PlanTag> tags, Vector2[] placed, List<int> done, int index)
    {
        Vector2 centre = tags[index].Centre;
        Vector2 size = tags[index].Size;
        for (int pass = 0; pass <= done.Count; pass++)
        {
            bool moved = false;
            foreach (int other in done)
            {
                Vector2 apart = Vector2.Abs(centre - placed[other]);
                Vector2 reach = (size + tags[other].Size) / 2;
                if (apart.X < reach.X && apart.Y < reach.Y)
                {
                    centre.Y = placed[other].Y + reach.Y + Gap;
                    moved = true;
                }
            }

            if (!moved)
            {
                break;
            }
        }

        return centre;
    }
}
