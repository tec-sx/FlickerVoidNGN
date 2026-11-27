using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Audio.Devices;
using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Audio.Sounds;
using FlickerVoid.Engine.Cameras;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Audio.Tests.Sounds;

/// <summary>
/// A playing game with rendering's components, cameras' trigger areas and audio over a temporary content folder, heard through a null device: writes
/// sounds of known samples, spawns entities from scene JSON and runs frames.
/// </summary>
internal sealed class AudioSystemTestsBuilder : IDisposable
{
    private readonly string _folder;
    private readonly DataAssets _data;

    public AudioSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
        ContentManager content = new(_folder);
        Game.Services.AddService<IContentManager>(content);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Game.AddRendering();
        Game.AddCameras();
        Audio = Game.AddAudio();
        Audio.SteamAudio = null;
        Music = Game.Services.GetService<MusicSystem>()!;
        Device = new NullAudioDevice(Audio.Mixer);
        Game.Lifecycle.Play(Game.World);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public AudioSystem Audio { get; }

    public MusicSystem Music { get; }

    public NullAudioDevice Device { get; }

    /// <summary>A context for effects and flow runs, with the game's facts and services.</summary>
    public ConditionContext Context => new(Game.Facts) { Services = Game.Services };

    /// <summary>
    /// Writes a sound whose variations are one-second mono files each holding one of the values throughout, with any more
    /// settings as JSON members (<c>"loop": true</c>), and returns its content path.
    /// </summary>
    public string WithSound(string name, string settings = "", params float[] values)
    {
        List<string> files = [];
        for (int index = 0; index < values.Length; index++)
        {
            string file = $"{name}{index}.wav";
            byte[] wav = WavFile.Constant(48000, values[index]);
            Write(file, wav);
            files.Add($"\"{file}\"");
        }

        string list = string.Join(", ", files);
        string more = settings.Length > 0 ? ", " + settings : string.Empty;
        string json = $$"""{ "type": "sound", "files": [ {{list}} ]{{more}} }""";
        return WithSoundJson(name, json);
    }

    /// <summary>A second of noise as a sound at a place a listener can tell the direction of, and returns its content path.</summary>
    public string WithNoise(string name)
    {
        Random random = new(3);
        float[] samples = new float[48000];
        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] = ((random.NextSingle() * 2) - 1) * 0.5f;
        }

        Write($"{name}0.wav", WavFile.Pcm16(1, 48000, samples));
        return WithSoundJson(name, $$"""{ "type": "sound", "files": [ "{{name}}0.wav" ], "loop": true }""");
    }

    /// <summary>Writes a sound data asset as given and returns its content path.</summary>
    public string WithSoundJson(string name, string json)
    {
        string path = $"{name}.fvdata";
        string fullPath = Path.Combine(_folder, path);
        File.WriteAllText(fullPath, json);
        return path;
    }

    public void Write(string path, byte[] contents)
    {
        string fullPath = Path.Combine(_folder, path);
        File.WriteAllBytes(fullPath, contents);
    }

    /// <summary>Spawns an entity from its scene JSON: <c>{ "name": ..., "components": { ... } }</c>.</summary>
    public Entity Spawn(string entityJson)
    {
        string json = $$"""{ "entities": [ {{entityJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        return Game.Spawner.Spawn(scene, Game.World)[0];
    }

    /// <summary>A listener with its ears at its feet at a place on the X axis.</summary>
    public Entity SpawnListener(float x = 0)
    {
        string json = $$"""{ "name": "Listener", "components": { "transform": { "position": [{{x}}, 0, 0] }, "audioListener": { "height": 0 } } }""";
        return Spawn(json);
    }

    public void Run(float seconds = 1 / 60f)
    {
        int frames = (int)MathF.Ceiling(seconds * 60);
        for (int frame = 0; frame < frames; frame++)
        {
            Game.Tick(GameLoop.DefaultFixedStep);
        }
    }

    /// <summary>The left and right samples the device plays after the next stretch of frames, once gains have settled.</summary>
    public (float Left, float Right) Hear(int frames = AudioFormat.BlockFrames * 2)
    {
        float[] heard = Device.Render(frames);
        return (heard[^2], heard[^1]);
    }

    /// <summary>Turns Steam Audio on for the sounds played from now on; false when its library is not built here.</summary>
    public bool UseSteamAudio()
    {
        Audio.SteamAudio = FlickerVoid.Engine.Audio.Spatial.SteamAudio.TryCreate();
        return Audio.SteamAudio is not null;
    }

    /// <summary>The summed loudness at each ear over a stretch of frames.</summary>
    public (double Left, double Right) HearEnergy(int frames = AudioFormat.BlockFrames * 8)
    {
        float[] heard = Device.Render(frames);
        double left = 0;
        double right = 0;
        for (int index = 0; index < heard.Length; index += 2)
        {
            left += Math.Abs(heard[index]);
            right += Math.Abs(heard[index + 1]);
        }

        return (left, right);
    }

    public void Dispose()
    {
        Game.Dispose();
        _data.Dispose();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
