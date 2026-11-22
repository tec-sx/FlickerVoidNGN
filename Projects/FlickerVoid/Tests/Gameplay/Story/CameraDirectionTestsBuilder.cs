using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Characters.Movement;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Gameplay.Tests.Story;

/// <summary>Plays the main scene as <see cref="StoryLoopTestsBuilder"/> does, and moves the fox straight to places to see what the camera does.</summary>
internal sealed class CameraDirectionTestsBuilder : IDisposable
{
    public StoryLoopTestsBuilder Story { get; } = new();

    public CameraDirector Director => Story.Game.Services.GetService<CameraDirector>()!;

    public CameraDirectionTestsBuilder Start()
    {
        Story.Start();
        return this;
    }

    public Entity Shot(string name) => Story.Find(name);

    /// <summary>Puts the fox at a place on the floor, stopping whatever walk it was on, and plays a few frames.</summary>
    public void PutFox(float x, float z)
    {
        Entity fox = Story.Fox;
        Story.Game.Events.Send(new StopCommand(fox));
        ref Transform transform = ref Story.Game.World.Get<Transform>(fox);
        transform.Position = new Vector3(x, 0, z);
        Story.Run(0.1f);
    }

    /// <summary>Talks the innkeeper into the cellar key quest, then takes the key.</summary>
    public void FindTheKey()
    {
        Story.Click("Innkeeper", () => Story.Dialogue.IsInConversation);
        Story.Choose("Anything I can do");
        Story.FinishConversation();
        Story.Click("Cellar Key", () => Story.Inventories.FindItemWithTag(Story.Fox, new("Key.Cellar")) is not null);
    }

    public bool RunUntilLive(string shot, float seconds)
    {
        Entity entity = Shot(shot);
        return Story.RunUntil(() => Director.Live == entity, seconds);
    }

    public void Dispose() => Story.Dispose();
}
