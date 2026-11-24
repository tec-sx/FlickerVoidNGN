using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>
/// Moves the game from scene to scene (a service): unloads the scene loaded, loads and starts the next, and stands its
/// <see cref="Traveller"/>s at the entry named, an entity of the next scene. Saves keep what the scenes left were like and carry the
/// travellers' state along, and loading a save travels to the scene it was made in.
/// </summary>
public sealed class SceneTravel
{
    private static readonly QueryDescription Travellers = new QueryDescription().WithAll<Traveller, Transform>();

    private readonly Game _game;
    private readonly SceneLoader _scenes;

    public SceneTravel(Game game, SceneLoader scenes)
    {
        _game = game;
        _scenes = scenes;
    }

    /// <summary>The scene loaded, or null before the first travel.</summary>
    public string? Current { get; private set; }

    /// <summary>Raised with the loaded scene's path before it unloads, while its entities are still there.</summary>
    public event Action<string>? Leaving;

    /// <summary>Raised with the next scene's path once it has spawned and started, before its travellers stand at the entry.</summary>
    public event Action<string>? Arriving;

    /// <summary>Raised with the next scene's path once its travellers stand at the entry.</summary>
    public event Action<string>? Arrived;

    /// <summary>
    /// Unloads the scene loaded and loads <paramref name="scene"/> now, standing the travellers at the entity named
    /// <paramref name="entry"/> when there is one; between ticks only, as it replaces entities systems may be using (see <see cref="Request"/>).
    /// </summary>
    public void Travel(string scene, string? entry = null)
    {
        if (Current is { } current)
        {
            Leaving?.Invoke(current);
            _scenes.Unload(current);
            Current = null;
        }

        IReadOnlyList<Entity> entities = _scenes.Load(scene);
        Current = scene;
        if (!_game.Lifecycle.IsPlaying)
        {
            _game.Lifecycle.Play(_game.World);
        }

        Arriving?.Invoke(scene);
        if (entry is not null)
        {
            StandAt(entities, entry);
        }

        Arrived?.Invoke(scene);
    }

    /// <summary>Travels before the next tick, outside every system, as effects and flow nodes ask to, then calls <paramref name="arrived"/>.</summary>
    public void Request(string scene, string? entry = null, Action? arrived = null)
    {
        _game.Defer(() =>
        {
            Travel(scene, entry);
            arrived?.Invoke();
        });
    }

    private void StandAt(IReadOnlyList<Entity> entities, string entry)
    {
        World world = _game.World;
        foreach (Entity entity in entities)
        {
            if (!world.IsAlive(entity) || !world.TryGet(entity, out EntityName name) || name.Value != entry || !world.TryGet(entity, out Transform place))
            {
                continue;
            }

            world.Query(in Travellers, (ref Transform transform) =>
            {
                transform.Position = place.Position;
                transform.Rotation = place.Rotation;
            });
            return;
        }
    }
}
