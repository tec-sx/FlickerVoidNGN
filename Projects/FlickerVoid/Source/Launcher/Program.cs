using FlickerVoid.Engine.Abilities.Activation;
using FlickerVoid.Engine.Attributes.Values;
using FlickerVoid.Engine.Debug;
using FlickerVoid.Engine.Dialogue.Conversations;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Hosting;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Story.Knowledge;
using FlickerVoid.Engine.Story.Quests;
using FlickerVoid.Engine.Ui;
using FlickerVoid.Engine.Ui.Stages;
using FlickerVoid.Engine.Ui.Surfaces;
using FlickerVoid.Gameplay;
using FlickerVoid.Interface;
using FlickerVoid.Launcher;

using Microsoft.Extensions.Logging;

// Runs the game in a window on its startup scene, with its interface over it; Escape closes it. "--scene <file>" runs another scene (the editor's Play does),
// "--mood <file>" lights it with a lighting mood, "--frames <count>" closes it after that many frames, "--saves <folder>" keeps save slots there,
// "--validation" checks Vulkan calls and "--debug" starts with the debug interface shown (F1 toggles it; its Game menu has the Story and Rules panels).
HostOptions options;
try
{
    options = HostOptions.Parse(args, "Scenes/Main.fvscene");
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

using ILoggerFactory loggerFactory = LoggerFactory.Create(static logging => logging.AddSimpleConsole());
using SceneHost host = new(options, loggerFactory, "FlickerVoid");
GameplayModule gameplay = new();
gameplay.Add(host.Game);
InterfaceModule ui = new();
ui.Add(host.Game);
host.Input.Map = host.Data.Load<InputMap>("Input/Controls.fvdata");
host.Start(CreateOverlays);
host.Run();
return 0;

// The game's interface, with the debug interface over it; clicks on either never reach the world.
FrameOverlays CreateOverlays(PixelFormat format, SceneRenderer renderer)
{
    UiPlatform.Start<InterfaceApplication>();
    host.Game.AddStages(host.Device, host.Content);
    GameInterface gameInterface = new(host.Game);
    host.Game.FrameSystems.Add(gameInterface);
    GameUi gameUi = new(host.Device, host.Window, format, gameInterface.View);
    DebugUi debugUi = CreateDebugUi(format, renderer);
    host.Input.PointerCaptures.Add(gameUi);
    host.Input.PointerCaptures.Add(debugUi);
    return new FrameOverlays([gameUi, debugUi]);
}

DebugUi CreateDebugUi(PixelFormat format, SceneRenderer renderer)
{
    string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlickerVoid", "FlickerVoid");
    DebugUi ui = new(host.Device, host.Window, format, settings);
    ui.AddEngineWindows(host.Game, host.Device);
    ui.AddRenderingWindows(renderer, host.Game.World);
    DialogueSystem dialogue = host.Game.Services.GetService<DialogueSystem>()!;
    QuestSystem quests = host.Game.Services.GetService<QuestSystem>()!;
    KnowledgeSystem knowledge = host.Game.Services.GetService<KnowledgeSystem>()!;
    StoryWindow story = new(dialogue, quests, knowledge, host.Data, host.Game.World) { IsOpen = true };
    ui.Windows.Add(story);
    AttributeSystem attributes = host.Game.Services.GetService<AttributeSystem>()!;
    AbilitySystem abilities = host.Game.Services.GetService<AbilitySystem>()!;
    RulesWindow rules = new(attributes, abilities, quests, host.Game.Events) { IsOpen = true };
    ui.Windows.Add(rules);
    ui.LoadLayout();
    ui.Visible = options.Debug;
    return ui;
}
