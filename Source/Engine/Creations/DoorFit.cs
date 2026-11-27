using System.Numerics;

namespace FlickerVoid.Engine.Creations;

/// <summary>
/// How a placed door sits on a wall, in the wall part's own space: where its floor line is on the wall's face, which way the face
/// points, and the numbers of the door, its cut, frame, window and grab point. The wall keeps its uncut solid, so the door can be
/// changed or removed later and the cut made again.
/// </summary>
public sealed class DoorFit
{
    public required string Wall { get; set; }

    /// <summary>The middle of the door's foot on the wall's face.</summary>
    public Vector3 Centre { get; set; }

    /// <summary>The wall face's normal, the way the door's front looks.</summary>
    public Vector3 Normal { get; set; } = Vector3.UnitZ;

    /// <summary>Along the face to the right of someone looking at it; up is the normal crossed with this.</summary>
    public Vector3 Right { get; set; } = Vector3.UnitX;

    public float Width { get; set; } = 0.9f;

    public float Height { get; set; } = 2;

    public float Thickness { get; set; } = 0.05f;

    public float Sill { get; set; }

    /// <summary>How much larger than the door the cut is on every side.</summary>
    public float Margin { get; set; } = 0.04f;

    /// <summary>How far the cut reaches into the wall, behind its face; zero cuts the wall through whatever its thickness.</summary>
    public float DepthIn { get; set; } = 0.3f;

    /// <summary>How far the cut reaches out of the wall's face.</summary>
    public float DepthOut { get; set; } = 0.1f;

    public bool HingeLeft { get; set; }

    /// <summary>Whether the door swings out of the face, or into the wall.</summary>
    public bool Outward { get; set; } = true;

    public float Angle { get; set; } = 90;

    /// <summary>The frame's width around the opening; zero has none.</summary>
    public float FrameWidth { get; set; }

    public float WindowWidth { get; set; }

    public float WindowHeight { get; set; } = 0.5f;

    /// <summary>The window's lower edge above the door's foot.</summary>
    public float WindowFrom { get; set; } = 1.2f;

    /// <summary>Whether the window has a pane of glass, a thin slab in its box.</summary>
    public bool Glass { get; set; }

    /// <summary>Height of the grab point, where a character's hand goes, above the door's foot.</summary>
    public float GrabHeight { get; set; } = 1;

    /// <summary>How far the grab point sits in from the door's free edge.</summary>
    public float GrabInset { get; set; } = 0.1f;

    /// <summary>A copy to edit without touching the placed door.</summary>
    public DoorFit Copy() => (DoorFit)MemberwiseClone();

    public Vector3 Up => Vector3.Normalize(Vector3.Cross(Normal, Right));

    public bool HasWindow => WindowWidth > 0;

    /// <summary>The grab point in the wall's space, on the door's front.</summary>
    public Vector3 GetGrab()
    {
        float side = HingeLeft ? 1 : -1;
        float across = side * ((Width / 2) - GrabInset);
        return Centre + (Up * (Sill + GrabHeight)) + (Right * across) - (Normal * (Thickness / 2));
    }
}
