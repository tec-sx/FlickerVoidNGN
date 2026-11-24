using FlickerVoid.Engine.Cameras;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Editor.Core.Tests;

/// <summary>Adds directed cameras to the editor's game, as Bleeders' gameplay module does.</summary>
internal sealed class CamerasModule : IGameModule
{
    public void Add(Game game) => game.AddCameras();
}
