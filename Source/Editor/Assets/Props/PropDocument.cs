using System.Numerics;

using FlickerVoid.Editor.Assets.Creators;
using FlickerVoid.Engine.Props;
using FlickerVoid.Engine.Props.Serialization;

namespace FlickerVoid.Editor.Assets.Props;

/// <summary>A prop file being edited in the creator: each change is held unsaved, one undo step in the file's history.</summary>
public sealed class PropDocument(UnsavedContent content, string path)
    : CreationDocument<PropFile>(content, path, PropsJsonContext.Default.PropFile)
{
    /// <summary>The gap left between a new box and the parts already there.</summary>
    public const float Gap = 0.5f;

    public PropFile Prop => Value;

    /// <summary>Adds a box of the size beside the parts already there, on the ground; its name.</summary>
    public string AddBox(Vector3 size)
    {
        float right = 0;
        foreach (PropPart part in Prop.Parts)
        {
            float x = part.Part.Mesh.Points.Max(point => point.X) + part.Part.Place.Position.X;
            right = MathF.Max(right, x);
        }

        float at = Prop.Parts.Count == 0 ? 0 : right + Gap + (size.X / 2);
        string name = Prop.AddBox(size, new Vector3(at, 0, 0));
        Hold();
        return name;
    }

    /// <summary>Takes the part out with the doors placed on it; false for a name the prop lacks or its last part.</summary>
    public bool Remove(string name)
    {
        if (Prop.Parts.Count < 2 || !Prop.Remove(name))
        {
            return false;
        }

        Hold();
        return true;
    }
}
