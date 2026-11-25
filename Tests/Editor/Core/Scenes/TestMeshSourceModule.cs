using FlickerVoid.Editor.Core.Tests.Selection;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Editor.Core.Tests.Scenes;

/// <summary>A project's game module that makes meshes of its own, as a creations module does.</summary>
internal sealed class TestMeshSourceModule : IGameModule
{
    public void Add(Game game)
    {
        MeshSources meshes = game.Services.GetService<MeshSources>()!;
        meshes.Add(new TestMeshSource());
    }
}
