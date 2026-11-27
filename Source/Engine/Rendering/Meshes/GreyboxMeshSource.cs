using System.Numerics;

using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>Makes the boxes and shaped solids greybox mesh names name (<see cref="GreyboxMesh"/>).</summary>
internal sealed class GreyboxMeshSource : IMeshSource
{
    public MadeMesh? Make(string mesh, IContentManager content)
    {
        if (GreyboxMesh.TryGetSolid(mesh, out SolidMesh? solid))
        {
            ModelData shaped = SolidModel.Create(solid);
            return new MadeMesh(shaped, null);
        }

        if (!GreyboxMesh.TryGetSize(mesh, out Vector3 size))
        {
            return null;
        }

        ModelData model = GreyboxMesh.Create(size);
        return new MadeMesh(model, null);
    }
}
