using Arch.System;

using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Ui.Stages;

/// <summary>The game's stages: creates them on the game's device and content and updates them each frame (a frame system and service).</summary>
public sealed class StageSystem(IGraphicsDevice device, IContentManager content, ISceneSpawner spawner, ILoggerFactory loggerFactory) : ISystem<GameTime>, IDisposable
{
    private readonly List<Stage> _stages = [];

    public Stage Create(string scene)
    {
        Stage stage = new(device, content, spawner, scene, loggerFactory);
        _stages.Add(stage);
        return stage;
    }

    public void Initialize()
    {
    }

    public void BeforeUpdate(in GameTime t)
    {
    }

    public void Update(in GameTime t)
    {
        float seconds = (float)t.Elapsed.TotalSeconds;
        foreach (Stage stage in _stages)
        {
            stage.Update(seconds);
        }
    }

    public void AfterUpdate(in GameTime t)
    {
    }

    public void Dispose()
    {
        foreach (Stage stage in _stages)
        {
            stage.Dispose();
        }

        _stages.Clear();
    }
}
