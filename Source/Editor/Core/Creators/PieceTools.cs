using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Meshes.Booleans;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Greyboxes;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Creators;

/// <summary>The level's own tools on its greyboxes: cutting some out of another, for a tunnel through a hill or an opening in a wall.</summary>
public static class PieceTools
{
    /// <summary>The least a cutter must take away to count as a cut, in cubic metres: a Boolean of two solids that don't touch still hands back a new solid.</summary>
    private const float MinimumCut = 1e-5f;

    /// <summary>Whether the entity is a greybox the mesh tools can shape: one that shows itself, not a swap model.</summary>
    public static bool IsPiece(World world, Entity entity)
    {
        return world.IsAlive(entity) && world.TryGet(entity, out Greybox greybox) && world.Has<Transform>(entity) && greybox.GetShownSwap() is null;
    }

    /// <summary>
    /// Cuts the <paramref name="cutters"/> out of <paramref name="target"/> where they overlap it, as they stand in the level, and
    /// leaves them where they are; one undo step of the level. False when nothing was cut: Booleans are missing, an entity is no
    /// greybox, or none of them touches the target.
    /// </summary>
    public static bool Cut(EditorScene scene, Entity target, IReadOnlyList<Entity> cutters)
    {
        World world = scene.Game.World;
        if (!SolidBoolean.IsAvailable || !IsPiece(world, target))
        {
            return false;
        }

        PieceShape shape = new(scene, target);
        SolidMesh? start = shape.Find();
        Matrix4x4 targetWorld = world.Get<Transform>(target).ToMatrix();
        if (start is null || !Matrix4x4.Invert(targetWorld, out Matrix4x4 toTarget))
        {
            return false;
        }

        SolidMesh solid = start;
        foreach (Entity cutter in cutters)
        {
            if (cutter == target || !IsPiece(world, cutter))
            {
                continue;
            }

            PieceShape cutterShape = new(scene, cutter);
            SolidMesh? own = cutterShape.Find();
            if (own is null)
            {
                continue;
            }

            Matrix4x4 placed = world.Get<Transform>(cutter).ToMatrix() * toTarget;
            SolidMesh inTarget = own.Transform(placed);
            SolidMesh? cut = SolidBoolean.Combine(solid, inTarget, BooleanOperation.Subtract);
            if (cut is not null && cut.Points.Count > 0 && MeasureVolume(cut) < MeasureVolume(solid) - MinimumCut)
            {
                solid = cut;
            }
        }

        if (ReferenceEquals(solid, start))
        {
            return false;
        }

        shape.Apply(solid);
        return true;
    }

    /// <summary>The volume a closed solid encloses, in cubic metres.</summary>
    private static float MeasureVolume(SolidMesh solid)
    {
        SolidTriangles triangles = solid.Triangulate();
        float sixfold = 0;
        for (int index = 0; index + 2 < triangles.Indices.Length; index += 3)
        {
            Vector3 first = triangles.Vertices[triangles.Indices[index]].Position;
            Vector3 second = triangles.Vertices[triangles.Indices[index + 1]].Position;
            Vector3 third = triangles.Vertices[triangles.Indices[index + 2]].Position;
            sixfold += Vector3.Dot(first, Vector3.Cross(second, third));
        }

        return MathF.Abs(sixfold) / 6;
    }
}
