using System.Text;

using Arch.Core;

using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Runtime;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging;

// Spawns the bodies of a scene file, prints how high each one is every half second of game time for three seconds,
// and fails if a body didn't fall.
const int StepsPerReport = 30;
const int Reports = 6;

string contentRoot = Path.Combine(AppContext.BaseDirectory, "Content");
IContentManager content = new ContentManager(contentRoot);
using ILoggerFactory loggerFactory = LoggerFactory.Create(static logging => logging.AddSimpleConsole());
using Game game = new(loggerFactory);
game.Services.AddService(content);
game.AddPhysics();

SceneData scene = content.Load("Scenes/FallingBodies.scene.json", RuntimeJsonContext.Default.SceneData);
IReadOnlyList<Entity> entities = game.Spawner.Spawn(scene, game.World);
List<Entity> bodies = [.. entities.Where(entity => game.World.Has<RigidBody>(entity))];
List<float> startHeights = [.. bodies.Select(body => game.World.Get<Transform>(body).Position.Y)];

TimeSpan time = TimeSpan.Zero;
PrintHeights(game.World, bodies, time);
for (int report = 0; report < Reports; report++)
{
    for (int step = 0; step < StepsPerReport; step++)
    {
        game.Tick(game.Loop.FixedStep);
        time += game.Loop.FixedStep;
    }

    PrintHeights(game.World, bodies, time);
}

for (int i = 0; i < bodies.Count; i++)
{
    Transform transform = game.World.Get<Transform>(bodies[i]);
    if (transform.Position.Y >= startHeights[i])
    {
        EntityName name = game.World.Get<EntityName>(bodies[i]);
        Console.Error.WriteLine($"{name.Value} didn't fall.");
        return 1;
    }
}

return 0;

static void PrintHeights(World world, List<Entity> bodies, TimeSpan time)
{
    StringBuilder line = new();
    line.Append($"{time.TotalSeconds:F1} s");
    foreach (Entity body in bodies)
    {
        EntityName name = world.Get<EntityName>(body);
        Transform transform = world.Get<Transform>(body);
        line.Append($"   {name.Value} {transform.Position.Y,5:F2}");
    }

    Console.WriteLine(line);
}
