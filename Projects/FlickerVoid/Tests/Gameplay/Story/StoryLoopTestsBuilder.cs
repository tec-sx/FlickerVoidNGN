using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Dialogue.Conversations;
using FlickerVoid.Engine.Flow;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Maps.Navigation;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Story.Quests;
using FlickerVoid.Gameplay.Protagonist.Control;

using Microsoft.Extensions.Logging.Abstractions;

using Silk.NET.Maths;

namespace FlickerVoid.Gameplay.Tests.Story;

/// <summary>
/// Plays FlickerVoid's main scene from the project's own content, as the launcher does without drawing: a 100 by 100 pixel window
/// with the mouse in its centre, clicks made by moving the camera straight over what is clicked, and saves in a temporary folder.
/// It can start the game again over the same saves, as a player restarting it.
/// </summary>
internal sealed class StoryLoopTestsBuilder : IDisposable
{
    private static readonly Quaternion LookingDown = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2);

    private readonly string _saves = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private DataAssets? _data;

    public Game Game { get; private set; } = null!;

    public DialogueSystem Dialogue => Game.Services.GetService<DialogueSystem>()!;

    public QuestSystem Quests => Game.Services.GetService<QuestSystem>()!;

    public InventorySystem Inventories => Game.Services.GetService<InventorySystem>()!;

    public MapSystem Maps => Game.Services.GetService<MapSystem>()!;

    public SaveSystem Saves => Game.Services.GetService<SaveSystem>()!;

    public Entity Fox => Find("Fox");

    /// <summary>Starts a new game on the main scene.</summary>
    public StoryLoopTestsBuilder Start()
    {
        Game?.Dispose();
        _data?.Dispose();
        Game = new Game(NullLoggerFactory.Instance);
        IGameWindow window = Substitute.For<IGameWindow>();
        window.Size.Returns(new Vector2D<int>(100, 100));
        IInputState input = Substitute.For<IInputState>();
        input.MousePosition.Returns(new Vector2(50, 50));
        Game.Services.AddService(window);
        Game.Services.AddService(input);
        string folder = FindContent();
        ContentManager content = new(folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Game.AddRendering();
        Game.AddFlow();
        FlowGraphs graphs = new(content, Game.Data);
        Game.Services.AddService(graphs);
        Game.AddSaves(new SaveSettings { Folder = _saves });
        GameplayModule gameplay = new();
        gameplay.Add(Game);
        SceneLoader scenes = new(Game.Spawner, content, Game.World, NullLogger<SceneLoader>.Instance);
        scenes.Load("Scenes/Main.fvscene");
        Game.Lifecycle.Play(Game.World);
        Run(0.1f);
        return this;
    }

    public Entity Find(string name)
    {
        Entity found = Entity.Null;
        QueryDescription named = new QueryDescription().WithAll<EntityName>();
        Game.World.Query(in named, (Entity entity, ref EntityName entityName) =>
        {
            if (entityName.Value == name)
            {
                found = entity;
            }
        });
        return found;
    }

    /// <summary>
    /// Clicks the entity, <paramref name="offset"/> from its position in its own space (a door's panel beside its hinge), and plays
    /// until <paramref name="done"/> holds, at most <paramref name="seconds"/>.
    /// </summary>
    public bool Click(string name, Func<bool> done, Vector3 offset = default, float seconds = 10)
    {
        Entity target = Find(name);
        Transform place = Game.World.Get<Transform>(target);
        Vector3 turned = Vector3.Transform(offset, place.Rotation);
        Vector3 point = place.Position + turned;
        Entity camera = Find("Camera");
        ref Transform view = ref Game.World.Get<Transform>(camera);
        view.Position = new Vector3(point.X, 10, point.Z);
        view.Rotation = LookingDown;
        Game.Events.Send(new InputActionEvent(ClickControlSystem.Use, InputPhase.Pressed));
        Game.Events.Dispatch();
        return RunUntil(done, seconds);
    }

    /// <summary>Picks the offered choice whose text starts with <paramref name="text"/>, once the conversation offers it.</summary>
    public bool Choose(string text)
    {
        RunUntil(() => Dialogue.Choices.Any(choice => choice.Text.StartsWith(text, StringComparison.Ordinal)), 10);
        foreach (DialogueChoice choice in Dialogue.Choices)
        {
            if (choice.Text.StartsWith(text, StringComparison.Ordinal))
            {
                return Dialogue.Choose(choice.Index);
            }
        }

        return false;
    }

    /// <summary>Plays the conversation's lines to its end.</summary>
    public bool FinishConversation() => RunUntil(() => !Dialogue.IsInConversation, 30);

    public bool RunUntil(Func<bool> done, float seconds)
    {
        int steps = (int)MathF.Ceiling(seconds * 60);
        for (int step = 0; step < steps && !done(); step++)
        {
            Game.Tick(Game.Loop.FixedStep);
        }

        return done();
    }

    public void Run(float seconds) => RunUntil(static () => false, seconds);

    public void Dispose()
    {
        Game?.Dispose();
        _data?.Dispose();
        if (Directory.Exists(_saves))
        {
            Directory.Delete(_saves, recursive: true);
        }
    }

    /// <summary>The project's source content folder, found above the test's output.</summary>
    private static string FindContent()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        while (folder is not null)
        {
            string content = Path.Combine(folder.FullName, "Content");
            string scene = Path.Combine(content, "Scenes", "Main.fvscene");
            if (File.Exists(scene))
            {
                return content;
            }

            folder = folder.Parent;
        }

        throw new DirectoryNotFoundException("No Content/Scenes/Main.fvscene above " + AppContext.BaseDirectory);
    }
}
