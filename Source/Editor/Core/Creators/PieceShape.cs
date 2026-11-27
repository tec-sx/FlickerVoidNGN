using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Runtime.Greyboxes;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Creators;

/// <summary>The shape of a level's greybox, as the mesh tools read and write it: the solid it keeps, or the box of its size.</summary>
public sealed class PieceShape(EditorScene scene, Entity entity)
{
    private SolidMesh? _box;
    private Vector3 _boxSize;

    /// <summary>The solid the greybox is: its shape, else the box of its size (the same one while the size stays); null when the entity is gone or no greybox.</summary>
    public SolidMesh? Find()
    {
        World world = scene.Game.World;
        if (!world.IsAlive(entity) || !world.TryGet(entity, out Greybox greybox))
        {
            return null;
        }

        if (greybox.Mesh is { } shape)
        {
            return shape;
        }

        if (_box is null || _boxSize != greybox.Size)
        {
            _box = SolidMesh.Box(greybox.Size);
            _boxSize = greybox.Size;
        }

        return _box;
    }

    /// <summary>Gives the greybox the shape, one undo step of the level.</summary>
    public void Apply(SolidMesh mesh)
    {
        World world = scene.Game.World;
        if (!world.IsAlive(entity) || !world.TryGet(entity, out Greybox greybox))
        {
            return;
        }

        greybox.Mesh = mesh;
        scene.SetComponent(entity, greybox);
    }
}
