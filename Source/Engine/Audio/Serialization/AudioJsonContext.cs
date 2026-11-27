using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Audio.Scripting;
using FlickerVoid.Engine.Audio.Sounds;

namespace FlickerVoid.Engine.Audio.Serialization;

/// <summary>Source-generated JSON for the audio components scenes hold and the sounds, effects and nodes data assets and flows hold.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AudioListener))]
[JsonSerializable(typeof(SoundEmitter))]
[JsonSerializable(typeof(SoundDefinition))]
[JsonSerializable(typeof(PlaySoundEffect))]
[JsonSerializable(typeof(PlaySoundNode))]
public sealed partial class AudioJsonContext : JsonSerializerContext;
