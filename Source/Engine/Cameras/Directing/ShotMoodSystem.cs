using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>
/// Lays the live shot's mood over the scene's and the story's (a frame system after the director), changing with the shot as
/// its transition does: at once on a cut or while a fade is dark, over the duration of a blend or cross-fade. Does nothing in
/// a game without a <see cref="MoodSystem"/>.
/// </summary>
public sealed class ShotMoodSystem : BaseSystem<World, GameTime>
{
    private readonly Game _game;
    private readonly CameraDirector _director;
    private Entity _shot = Entity.Null;
    private string? _mood;

    public ShotMoodSystem(Game game, CameraDirector director)
        : base(game.World)
    {
        _game = game;
        _director = director;
    }

    public override void Update(in GameTime t)
    {
        MoodSystem? moods = _game.Services.GetService<MoodSystem>();
        if (moods is null)
        {
            return;
        }

        Entity live = _director.Live;
        string? mood = FindMood(live);
        if (live == _shot && mood == _mood)
        {
            return;
        }

        float seconds = 0;
        if (live != _shot)
        {
            seconds = _director.SwitchSeconds;
        }

        _shot = live;
        _mood = mood;
        moods.SetShotMood(mood, seconds);
    }

    private string? FindMood(Entity shot)
    {
        if (shot == Entity.Null || !World.IsAlive(shot) || !World.TryGet(shot, out Shot found))
        {
            return null;
        }

        if (string.IsNullOrEmpty(found.Mood))
        {
            return null;
        }

        return found.Mood;
    }
}
