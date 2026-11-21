namespace FlickerVoid.Editor.Core.Menus;

/// <summary>What a row of a searchable menu is.</summary>
public enum MenuRowKind
{
    /// <summary>A choice; activating it runs it.</summary>
    Choice,

    /// <summary>The name of the group whose choices follow; not selectable.</summary>
    Heading,

    /// <summary>A group shown as a submenu; activating it opens it.</summary>
    Group,

    /// <summary>Back from an open group to the groups.</summary>
    Back,
}

/// <summary>A row of a searchable menu; <see cref="Detail"/> names a found choice's group.</summary>
public sealed record MenuRow(MenuRowKind Kind, string Text, string? Icon = null, MenuChoice? Choice = null, string? Detail = null)
{
    public bool IsSelectable => Kind != MenuRowKind.Heading;

    public bool IsHeading => Kind == MenuRowKind.Heading;

    public bool IsGroup => Kind == MenuRowKind.Group;

    public bool IsBack => Kind == MenuRowKind.Back;

    public string? Gesture => Choice?.Gesture;
}
