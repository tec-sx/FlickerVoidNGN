using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>Adds saves to a game, as a service.</summary>
public static class SaveGameExtensions
{
    public static SaveSystem AddSaves(this Game game, SaveSettings settings)
    {
        SaveSystem saves = new(game, settings);
        game.Services.AddService(saves);
        return saves;
    }
}
