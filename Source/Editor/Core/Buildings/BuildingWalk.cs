using System.Globalization;
using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Ai;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Cameras;
using FlickerVoid.Engine.Cameras.Cutaways;
using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Characters;
using FlickerVoid.Engine.Characters.Movement;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction;
using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Interaction.Interactors;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Core.Buildings;

/// <summary>
/// The building creator's Walk: the building played on its own, as the game plays it, in a world of its own on a wide ground, with a
/// walker standing outside its first door. A click on the ground walks there; a click on a door, an elevator's platform or a call
/// button walks to it and uses it, so doors open and shut (a locked one rattles), the platform rides and calls bring it. The building's
/// shots switch as their triggers say; outside, a shot follows the walker.
/// </summary>
public sealed class BuildingWalk : IDisposable
{
    private const float Reach = 0.6f;
    private const float StartDistance = 2;
    private const float RayLength = 500;
    private const float ButtonReach = 0.4f;

    /// <summary>How near a platform's middle the walker stands before riding it.</summary>
    private const float OnIt = 0.25f;
    private const int MaxUnseen = 8;
    private const float Step = 0.001f;

    private static readonly Tag ToggleAction = new("Interaction.Action.Toggle");

    private readonly IPhysicsSystem _physics;
    private readonly CameraDirector _director;
    private Entity _using = Entity.Null;

    /// <summary>Places the building file at the origin of a new playing world over the content, and the walker outside its first door.</summary>
    public BuildingWalk(IContentManager content, string file, BuildingFile building)
    {
        Game = new Game(NullLoggerFactory.Instance);
        Game.Services.AddService(content);
        Game.AddRendering();
        _physics = Game.AddPhysics();
        Game.AddCharacters();
        Game.AddAi();
        Game.AddInteraction();
        _director = Game.AddCameras();
        Game.AddBuildings();

        Vector3 start = GetStart(building);
        string name = JsonSerializer.Serialize(file, RuntimeJsonContext.Default.String);
        Spawn("""{ "name": "Walk Ground", "components": { "transform": { "position": [0, -0.1, 0] }, "staticBody": { "collider": { "type": "box", "size": [80, 0.2, 80] } }, "greybox": { "size": [80, 0.2, 80] } } }""");
        Building = Spawn($$"""{ "name": "Walk Building", "components": { "transform": {}, "building": { "file": {{name}} } } }""");
        Walker = Spawn($$"""{ "name": "Walker", "components": { "transform": { "position": [{{Format(start.X)}}, {{Format(start.Y)}}, {{Format(start.Z)}}] }, "characterMover": { "speed": 2.5 }, "triggerActivator": {}, "cameraTarget": {} } }""");
        Spawn("""{ "name": "Walker Look", "parent": "Walker", "components": { "transform": { "position": [0, 0.85, 0] }, "greybox": { "size": [0.45, 1.7, 0.45] } } }""");
        Entity outside = Spawn("""{ "name": "Walk Outside", "components": { "transform": {}, "camera": {}, "shot": { "start": true } } }""");
        Quaternion down = Quaternion.CreateFromYawPitchRoll(0, -MathF.PI / 4, 0);
        World.Set(outside, new Transform { Position = start + new Vector3(0, 14, -14), Rotation = down });
        World.Set(outside, new Camera { Projection = CameraProjection.Orthographic, OrthographicHeight = 12, FarPlane = 200 });
        Game.Lifecycle.Play(Game.World);
        Run(0.2f);
    }

    public Game Game { get; }

    public World World => Game.World;

    /// <summary>The placed building, its parts nested under it.</summary>
    public Entity Building { get; }

    public Entity Walker { get; }

    /// <summary>The shot drawn now and where it looks from: the building's shot its trigger switched to, else the one outside.</summary>
    public RenderCamera? View
    {
        get
        {
            if (_director.Live == Entity.Null || !World.TryGet(_director.Live, out ActiveCamera active))
            {
                return null;
            }

            Transform pose = active.Pose;
            return new RenderCamera(active.Lens, pose);
        }
    }

    /// <summary>The name of the shot drawn now.</summary>
    public string LiveShot
    {
        get
        {
            if (_director.Live == Entity.Null || !World.TryGet(_director.Live, out EntityName name))
            {
                return string.Empty;
            }

            return name.Value;
        }
    }

    /// <summary>Walks to what the ray hits first: the ground or a floor, or a door, platform or call button to use once reached.</summary>
    public void Click(Ray ray)
    {
        Entity target = FindUsable(ray);
        if (target != Entity.Null)
        {
            _using = target;
            Vector3 point = InteractionSystem.GetUsePoint(World, target);
            bool onto = IsStoodOn(target);
            Game.Events.Send(new MoveCommand(Walker, point, onto));
            return;
        }

        if (!PickSeen(ray, out RayHit hit))
        {
            return;
        }

        _using = Entity.Null;
        Game.Events.Send(new MoveCommand(Walker, hit.Point));
    }

    /// <summary>Runs the world for the seconds that passed, using what was clicked once the walker reaches it.</summary>
    public void Update(float seconds)
    {
        Game.Tick(TimeSpan.FromSeconds(Math.Clamp(seconds, 0, 0.1f)));
        if (_using == Entity.Null || !World.IsAlive(_using))
        {
            return;
        }

        Vector3 at = World.Get<Transform>(Walker).Position;
        Vector3 point = InteractionSystem.GetUsePoint(World, _using);
        Vector2 apart = new(at.X - point.X, at.Z - point.Z);
        float reach = Reach;
        if (IsStoodOn(_using))
        {
            reach = OnIt;
        }

        if (apart.Length() <= reach && MathF.Abs(at.Y - point.Y) < 1.5f)
        {
            Game.Events.Send(new InteractionActionEvent(Walker, _using, ToggleAction));
            _using = Entity.Null;
        }
    }

    /// <summary>Runs the world for the seconds in fixed steps, as tests do.</summary>
    public void Run(float seconds)
    {
        int steps = (int)MathF.Ceiling(seconds / (float)Game.Loop.FixedStep.TotalSeconds);
        for (int step = 0; step < steps; step++)
        {
            Update((float)Game.Loop.FixedStep.TotalSeconds);
        }
    }

    public void Dispose() => Game.Dispose();

    private bool IsStoodOn(Entity target) => World.TryGet(target, out UsePoint use) && use.StandOn;

    /// <summary>Outside the ground floor's first door, facing it; outside the front wall's middle when the building has no door.</summary>
    public static Vector3 GetStart(BuildingFile building)
    {
        BuildingStorey? ground = building.FindStorey(0) ?? building.Storeys.FirstOrDefault();
        if (ground is null)
        {
            return new Vector3(0, 0, -building.Size.Z);
        }

        PlanOpening? door = ground.Openings.Find(static opening => opening.Kind is OpeningKind.Door or OpeningKind.Doorway);
        PlanWall? wall = null;
        float along = 0;
        if (door is not null)
        {
            wall = PlanGeometry.FindWall(ground, door.Wall);
            along = door.Along;
        }

        wall ??= ground.Walls.Find(static found => found.Outside);
        if (wall is null)
        {
            return new Vector3(0, 0, -building.Size.Z);
        }

        List<Vector2> path = PlanGeometry.GetPath(ground, wall);
        if (door is null)
        {
            along = PlanGeometry.GetLength(path) / 2;
        }

        (Vector2 point, _) = PlanGeometry.GetAlong(path, along);
        Vector2 middle = Vector2.Zero;
        List<Vector2> outline = PlanGeometry.GetOutline(ground);
        if (outline.Count > 0)
        {
            middle = outline.Aggregate(Vector2.Zero, static (sum, corner) => sum + corner) / outline.Count;
        }

        Vector2 away = point - middle;
        if (away.LengthSquared() < 1e-6f)
        {
            away = -Vector2.UnitY;
        }

        Vector2 standing = point + (Vector2.Normalize(away) * StartDistance);
        return new Vector3(standing.X, 0, standing.Y);
    }

    private Entity FindUsable(Ray ray)
    {
        if (PickSeen(ray, out RayHit hit) && World.IsAlive(hit.Entity) && World.Has<Toggle>(hit.Entity))
        {
            return hit.Entity;
        }

        float limit = RayLength;
        if (hit.Entity != Entity.Null)
        {
            limit = hit.Distance + ButtonReach;
        }

        Entity nearest = Entity.Null;
        float best = float.MaxValue;
        World.Query(new QueryDescription().WithAll<Toggle, Transform>(), (Entity entity, ref Transform transform) =>
        {
            Vector3 offset = transform.Position - ray.Origin;
            float along = Vector3.Dot(offset, ray.Direction);
            if (along <= 0 || along > limit)
            {
                return;
            }

            Vector3 closest = ray.Origin + (ray.Direction * along);
            float apart = Vector3.Distance(closest, transform.Position);
            if (apart <= ButtonReach && along < best)
            {
                best = along;
                nearest = entity;
            }
        });
        return nearest;
    }

    /// <summary>The first hit the view can see: what the cutaway hides over the walker's storey is passed through, as the game does.</summary>
    private bool PickSeen(Ray ray, out RayHit hit)
    {
        if (!_physics.RayCast(ray, RayLength, Walker, out hit))
        {
            return false;
        }

        if (Game.Services.GetService<CutawaySystem>() is not { } cutaways)
        {
            return true;
        }

        float travelled = 0;
        for (int passed = 0; passed < MaxUnseen && cutaways.IsUnseen(hit.Entity, hit.Point, ray.Direction); passed++)
        {
            travelled += hit.Distance + Step;
            Vector3 origin = hit.Point + (ray.Direction * Step);
            Ray beyond = new(origin, ray.Direction);
            if (!_physics.RayCast(beyond, RayLength - travelled, hit.Entity, out hit))
            {
                return false;
            }
        }

        hit = hit with { Distance = hit.Distance + travelled };
        return true;
    }

    private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private Entity Spawn(string entityJson)
    {
        string json = $$"""{ "entities": [ {{entityJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        return Game.Spawner.Spawn(scene, Game.World)[0];
    }
}
