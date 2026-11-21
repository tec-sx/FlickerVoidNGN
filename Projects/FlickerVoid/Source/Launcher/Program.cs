using FlickerVoid.Engine.Debug;
using FlickerVoid.Engine.Dialogue.Conversations;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Hosting;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Story.Knowledge;
using FlickerVoid.Engine.Story.Quests;
using FlickerVoid.Gameplay;
using FlickerVoid.Launcher;

using Microsoft.Extensions.Logging;

// Runs the game in a window on its startup scene; Escape closes it. "--scene <file>" runs another scene (the editor's Play does),
// "--mood <file>" lights it with a lighting mood, "--frames <count>" closes it after that many frames, "--saves <folder>" keeps save slots there,
// "--validation" checks Vulkan calls and "--debug" starts with the debug interface shown (F1 toggles it; its Game menu has the Story panel).
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
host.Input.Map = host.Data.Load<InputMap>("Input/Controls.fvdata");
host.Start(CreateDebugUi);
host.Run();
return 0;

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
    ui.LoadLayout();
    ui.Visible = options.Debug;
    return ui;
}
