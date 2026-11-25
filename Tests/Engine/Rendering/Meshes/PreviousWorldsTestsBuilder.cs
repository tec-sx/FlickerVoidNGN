using System.Numerics;

using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Engine.Rendering.Tests.Meshes;

/// <summary>Builds the memory of meshes' world matrices and the matrices of meshes standing at given places.</summary>
internal sealed class PreviousWorldsTestsBuilder
{
    public static Matrix4x4 At(float x)
    {
        return Matrix4x4.CreateTranslation(x, 0, 0);
    }

    public PreviousWorlds Build()
    {
        return new PreviousWorlds();
    }
}
