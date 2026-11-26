using System.Numerics;

using FlickerVoid.Editor.Core.Creators;

namespace FlickerVoid.Editor.Core.Meshes;

/// <summary>What a mesh transform tool does with the values typed into the numeric bar.</summary>
public enum MeshTransformTool
{
    Move,
    Rotate,
    Scale,
}

/// <summary>
/// The numeric bar of the mesh's Move, Rotate and Scale: the tool and the three values typed for it, which <see cref="Apply"/> does
/// to the selected points as one undo step. Each tool starts from its neutral values: no offset, no turn, a factor of one.
/// </summary>
public sealed class MeshTransformBar
{
    private MeshTransformTool _tool;

    public MeshTransformBar()
    {
        Values = Vector3.Zero;
    }

    public MeshTransformTool Tool
    {
        get => _tool;
        set
        {
            _tool = value;
            Values = GetNeutral(value);
        }
    }

    /// <summary>The offset in metres, the angles in degrees about X, Y and Z, or the factor along each axis, as the tool takes them.</summary>
    public Vector3 Values { get; set; }

    /// <summary>Does the tool to the selection of the creator; false when nothing is selected. The values go back to neutral after it.</summary>
    public bool Apply(CreatorMode creator)
    {
        bool done;
        switch (Tool)
        {
            case MeshTransformTool.Move:
                done = creator.MoveMesh(Values);
                break;
            case MeshTransformTool.Rotate:
                done = creator.RotateMesh(Values);
                break;
            default:
                done = creator.ScaleMesh(Values);
                break;
        }

        if (done)
        {
            Values = GetNeutral(Tool);
        }

        return done;
    }

    private static Vector3 GetNeutral(MeshTransformTool tool)
    {
        if (tool == MeshTransformTool.Scale)
        {
            return Vector3.One;
        }

        return Vector3.Zero;
    }
}
