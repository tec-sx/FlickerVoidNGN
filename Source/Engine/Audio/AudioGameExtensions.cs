using FlickerVoid.Engine.Audio.Scripting;
using FlickerVoid.Engine.Audio.Serialization;
using FlickerVoid.Engine.Audio.Sounds;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Audio;

/// <summary>
/// Adds sound to a game: the <c>audioListener</c> and <c>soundEmitter</c> components, the <c>sound</c> data asset type, the
/// <c>playSound</c> effect and flow node, the <c>audio.</c> volumes on the console, and the audio system (a frame system and the
/// <see cref="IAudioEngine"/> service), whose mixer a host's device plays; without a device nothing is heard and everything else works.
/// </summary>
public static class AudioGameExtensions
{
    public static AudioSystem AddAudio(this Game game)
    {
        game.Spawner.Register("audioListener", AudioJsonContext.Default.AudioListener);
        game.Spawner.Register("soundEmitter", AudioJsonContext.Default.SoundEmitter);
        game.Data.AddResolver(AudioJsonContext.Default);
        game.Data.Register<SoundDefinition>("sound");
        game.Data.RegisterDerived<Effect, PlaySoundEffect>("playSound");
        game.Data.RegisterDerived<FlowNode, PlaySoundNode>("playSound");
        AudioSystem audio = new(game);
        audio.Settings.Register(game.DebugConsole);
        game.FrameSystems.Add(audio);
        game.Services.AddService(audio);
        game.Services.AddService<IAudioEngine>(audio);
        return audio;
    }
}
