using FlickerVoid.Editor.Assets.Creators;
using FlickerVoid.Editor.Core.Meshes.Doors;
using FlickerVoid.Engine.Foliage;
using FlickerVoid.Engine.Foliage.Serialization;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Assets.Foliage;

/// <summary>
/// A foliage file being edited in the creator: each change is held unsaved, one undo step in the file's history. A change of kind,
/// seed or shape grows the plant anew, replacing what the mesh tools made of its parts.
/// </summary>
public sealed class FoliageDocument(UnsavedContent content, string path)
    : CreationDocument<FoliageFile>(content, path, FoliageJsonContext.Default.FoliageFile)
{
    public FoliageFile Plant => Value;

    /// <summary>Makes it a tree or a bush of that kind's shape.</summary>
    public void SetKind(FoliageKind kind)
    {
        Plant.SetKind(kind);
        Hold();
    }

    /// <summary>Grows the plant from another seed.</summary>
    public void SetSeed(int seed)
    {
        Plant.Seed = seed;
        Plant.Grow();
        Hold();
    }

    /// <summary>Grows the plant from the shape; false, holding nothing, for a shape nothing grows from.</summary>
    public bool SetShape(FoliageShape shape)
    {
        if (!shape.IsValid())
        {
            return false;
        }

        Plant.Shape = shape;
        Plant.Grow();
        Hold();
        return true;
    }

    /// <summary>Plants have no doors, so no part is a wall for one.</summary>
    public override bool IsDoorWall(string partId) => false;
}
