using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Audio.Scripting;
using FlickerVoid.Engine.Audio.Serialization;
using FlickerVoid.Engine.Audio.Sounds;
using FlickerVoid.Engine.Audio.Spatial;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Audio;

/// <summary>
/// Adds sound to a game: the <c>audioListener</c> and <c>soundEmitter</c> components, the <c>sound</c> data asset type, the
/// <c>playSound</c> effect and flow node, the <c>audio.</c> volumes on the console, and the audio system (a frame system and the
/// <see cref="IAudioEngine"/> service), whose mixer a host's device plays; without a device nothing is heard and everything else
/// works. And layered music and ambience: the <c>music</c> data asset type, the <c>musicArea</c> and <c>sceneMusic</c> components,
/// the <c>playMusic</c> and <c>playStinger</c> effects and flow nodes, and the music system (a frame system and the
/// <see cref="IMusicPlayer"/> service); music areas need the cameras' trigger areas (<c>AddCameras</c>).
/// </summary>
public static class AudioGameExtensions
{
    public static AudioSystem AddAudio(this Game game)
    {
        game.Spawner.Register("audioListener", AudioJsonContext.Default.AudioListener);
        game.Spawner.Register("soundEmitter", AudioJsonContext.Default.SoundEmitter);
        game.Spawner.Register("roomReverb", AudioJsonContext.Default.RoomReverb);
        game.Data.AddResolver(AudioJsonContext.Default);
        game.Data.Register<SoundDefinition>("sound", SoundDefinition.Extension);
        game.Data.RegisterDerived<Effect, PlaySoundEffect>("playSound");
        game.Data.RegisterDerived<FlowNode, PlaySoundNode>("playSound");
        AudioSystem audio = new(game) { SteamAudio = SteamAudio.TryCreate() };
        audio.Settings.Register(game.DebugConsole);
        game.FrameSystems.Add(audio);
        game.Services.AddService(audio);
        game.Services.AddService<IAudioEngine>(audio);
        game.Spawner.Register("musicArea", AudioJsonContext.Default.MusicArea);
        game.Spawner.Register("sceneMusic", AudioJsonContext.Default.SceneMusic);
        game.Data.Register<MusicCue>("music", MusicCue.Extension);
        game.Data.RegisterDerived<Effect, PlayMusicEffect>("playMusic");
        game.Data.RegisterDerived<FlowNode, PlayMusicNode>("playMusic");
        game.Data.RegisterDerived<Effect, PlayStingerEffect>("playStinger");
        game.Data.RegisterDerived<FlowNode, PlayStingerNode>("playStinger");
        MusicSystem music = new(game, audio);
        game.FrameSystems.Add(music);
        game.Services.AddService(music);
        game.Services.AddService<IMusicPlayer>(music);
        return audio;
    }
}
