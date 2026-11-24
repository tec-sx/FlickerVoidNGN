using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>Adds scene travel to a game, as a service that saves follow.</summary>
public static class SceneTravelGameExtensions
{
    public static SceneTravel AddSceneTravel(this Game game, SceneLoader scenes)
    {
        SceneTravel travel = new(game, scenes);
        game.Services.AddService(travel);
        SaveSystem? saves = game.Services.GetService<SaveSystem>();
        saves?.Follow(travel);
        return travel;
    }
}
