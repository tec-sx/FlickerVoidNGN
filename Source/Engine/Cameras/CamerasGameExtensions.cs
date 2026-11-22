using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Cameras.Serialization;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Cameras;

/// <summary>Adds directed cameras to a game: the <c>shot</c> and <c>cameraTarget</c> components and the camera director (a frame system and service).</summary>
public static class CamerasGameExtensions
{
    public static CameraDirector AddCameras(this Game game)
    {
        game.Spawner.Register("shot", CamerasJsonContext.Default.Shot);
        game.Spawner.Register("cameraTarget", CamerasJsonContext.Default.CameraTarget);
        CameraDirector director = new(game.World, game.Events);
        game.FrameSystems.Add(director);
        game.Services.AddService(director);
        return director;
    }
}
