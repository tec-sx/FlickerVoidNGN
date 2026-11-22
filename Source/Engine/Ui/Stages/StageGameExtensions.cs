using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Ui.Serialization;

namespace FlickerVoid.Engine.Ui.Stages;

/// <summary>Adds turntables and stages to a game.</summary>
public static class StageGameExtensions
{
    /// <summary>Lets scene files add the <c>turntable</c> component, which spins in the game's world as in stages; a game module adds it, so the editor knows it too.</summary>
    public static void AddTurntables(this Game game)
    {
        game.Spawner.Register("turntable", UiJsonContext.Default.Turntable);
        TurntableSystem turntables = new(game.World);
        game.FrameSystems.Add(turntables);
    }

    /// <summary>Adds <see cref="StageSystem"/> as a frame system and service, drawing on the game's device; stage scenes may use turntables once <see cref="AddTurntables"/> ran.</summary>
    public static StageSystem AddStages(this Game game, IGraphicsDevice device, IContentManager content)
    {
        StageSystem stages = new(device, content, game.Spawner, game.LoggerFactory);
        game.FrameSystems.Add(stages);
        game.Services.AddService(stages);
        return stages;
    }
}
