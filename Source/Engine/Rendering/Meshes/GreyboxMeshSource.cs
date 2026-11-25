using System.Numerics;

using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>Makes the boxes greybox mesh names name (<see cref="GreyboxMesh"/>).</summary>
internal sealed class GreyboxMeshSource : IMeshSource
{
    public MadeMesh? Make(string mesh, IContentManager content)
    {
        if (!GreyboxMesh.TryGetSize(mesh, out Vector3 size))
        {
            return null;
        }

        ModelData model = GreyboxMesh.Create(size);
        return new MadeMesh(model, null);
    }
}
