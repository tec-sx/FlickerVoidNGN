using System.Text.Json;
using System.Text.Json.Nodes;

using Arch.Core;

using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>
/// Writes the game to save slots and reads it back: the facts, which hold quests, knowledge and standing, the <see cref="SaveStates"/>
/// of every <see cref="Saveable"/> entity (those gone when saving are destroyed when loading) and of the game as a whole; modules
/// that need to follow a load handle <see cref="Loading"/> and <see cref="Loaded"/>. With <see cref="SceneTravel"/> it also keeps the
/// scenes the game left as they were left, carries travellers' state to the next scene, and loads by travelling to the save's scene,
/// so the world is spawned anew before the save is read back. Each slot is a <c>.fvsave</c> JSON file in the settings' folder.
/// </summary>
public sealed partial class SaveSystem
{
    public const string Extension = ".fvsave";

    private static readonly QueryDescription Saveables = new QueryDescription().WithAll<Saveable>();
    private static readonly QueryDescription Travellers = new QueryDescription().WithAll<Saveable, Traveller>();

    private readonly World _world;
    private readonly EntityLifecycle _lifecycle;
    private readonly SaveStates _states;
    private readonly HashSet<string> _started = [];
    private readonly IFactDatabase _facts;
    private readonly SaveSettings _settings;
    private readonly ILogger<SaveSystem> _logger;
    private readonly HashSet<Tag> _blocks = [];
    private Dictionary<string, SavedScene> _visited = [];
    private Dictionary<string, Dictionary<string, JsonNode>> _carried = [];
    private SceneTravel? _travel;
    private bool _loading;
    private TimeSpan _total;
    private TimeSpan _totalAtLoad;
    private TimeSpan _loadedPlayTime;

    public SaveSystem(Game game, SaveSettings settings)
    {
        _world = game.World;
        _lifecycle = game.Lifecycle;
        _states = game.SaveStates;
        _facts = game.Facts;
        game.Lifecycle.OnActivate((Entity _, ref Saveable saveable) => _started.Add(saveable.Id));
        _settings = settings;
        _logger = game.LoggerFactory.CreateLogger<SaveSystem>();
        game.Loop.Update += time => _total = time.Total;
        SceneTravel? travel = game.Services.GetService<SceneTravel>();
        if (travel is not null)
        {
            Follow(travel);
        }
    }

    /// <summary>Raised once a save has been read, before the game travels to its scene and reads it back, so modules can let go of what they run.</summary>
    public event Action<SaveGame>? Loading;

    /// <summary>Raised after a save is read and the facts and states are back.</summary>
    public event Action<SaveGame>? Loaded;

    /// <summary>How long the game has been played, over every load.</summary>
    public TimeSpan PlayTime => _loadedPlayTime + (_total - _totalAtLoad);

    /// <summary>False while anything blocks saving, such as a conversation.</summary>
    public bool CanSave => _blocks.Count == 0;

    public SaveGame? LastLoaded { get; private set; }

    public void AddBlock(Tag reason) => _blocks.Add(reason);

    public void RemoveBlock(Tag reason) => _blocks.Remove(reason);

    public bool Save(string slot) => Write(slot, checkpoint: false);

    public bool SaveCheckpoint() => Write(_settings.CheckpointSlot, checkpoint: true);

    public bool LoadCheckpoint() => Load(_settings.CheckpointSlot);

    /// <summary>Keeps the scenes the travel leaves and carries its travellers along; <c>AddSceneTravel</c> and <c>AddSaves</c> call it.</summary>
    public void Follow(SceneTravel travel)
    {
        if (_travel is not null)
        {
            throw new InvalidOperationException("The saves already follow a scene travel.");
        }

        _travel = travel;
        travel.Leaving += HandleLeaving;
        travel.Arriving += HandleArriving;
    }

    /// <summary>Restores the slot; false when it is missing, unreadable or from a newer version, leaving the game as it was.</summary>
    public bool Load(string slot)
    {
        if (!TryGetPath(slot, out string path) || !File.Exists(path))
        {
            return false;
        }

        SaveGame? save = Read(path);
        if (save is null)
        {
            return false;
        }

        if (save.Metadata.Version > SaveGame.CurrentVersion)
        {
            LogNewer(slot, save.Metadata.Version);
            return false;
        }

        Loading?.Invoke(save);
        TravelTo(save);
        Dictionary<Tag, int> facts = [];
        foreach ((string name, int value) in save.Facts)
        {
            Tag tag = new(name);
            facts[tag] = value;
        }

        _facts.RestoreFacts(facts);
        _visited = new Dictionary<string, SavedScene>(save.Scenes);
        _carried = [];
        ReadEntities(save.Entities, save.Destroyed);
        ReadGlobals(save);
        _loadedPlayTime = save.Metadata.PlayTime;
        _totalAtLoad = _total;
        LastLoaded = save;
        Loaded?.Invoke(save);
        return true;
    }

    public bool Delete(string slot)
    {
        if (!TryGetPath(slot, out string path) || !File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    /// <summary>The metadata of every readable slot, newest first.</summary>
    public IReadOnlyList<SaveMetadata> GetSlots()
    {
        List<SaveMetadata> slots = [];
        if (!Directory.Exists(_settings.Folder))
        {
            return slots;
        }

        foreach (string path in Directory.EnumerateFiles(_settings.Folder, "*" + Extension))
        {
            SaveGame? save = Read(path);
            if (save is not null)
            {
                slots.Add(save.Metadata);
            }
        }

        slots.Sort(static (first, second) =>
        {
            int byTime = second.Timestamp.CompareTo(first.Timestamp);
            if (byTime != 0)
            {
                return byTime;
            }

            return second.PlayTime.CompareTo(first.PlayTime);
        });
        return slots;
    }

    private bool Write(string slot, bool checkpoint)
    {
        if (!CanSave || !TryGetPath(slot, out string path))
        {
            return false;
        }

        SaveGame save = new();
        save.Metadata.Version = SaveGame.CurrentVersion;
        save.Metadata.Slot = slot;
        save.Metadata.Timestamp = DateTimeOffset.Now;
        save.Metadata.PlayTime = PlayTime;
        save.Metadata.IsCheckpoint = checkpoint;
        foreach ((Tag tag, int value) in _facts.AllFacts)
        {
            save.Facts[tag.Name] = value;
        }

        save.Scene = _travel?.Current ?? string.Empty;
        WriteEntities(save.Entities, save.Destroyed, withTravellers: true);
        foreach ((string scene, SavedScene left) in _visited)
        {
            save.Scenes[scene] = left;
        }

        WriteGlobals(save);
        Directory.CreateDirectory(_settings.Folder);
        string temporary = path + ".tmp";
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(save, SavesJsonContext.Default.SaveGame);
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path, overwrite: true);
        return true;
    }

    /// <summary>Respawns the save's scene, or the scene loaded when the save names none, so nothing from before the load lingers.</summary>
    private void TravelTo(SaveGame save)
    {
        if (_travel is null)
        {
            return;
        }

        string? scene = save.Scene.Length > 0 ? save.Scene : _travel.Current;
        if (scene is null)
        {
            return;
        }

        _loading = true;
        try
        {
            _travel.Travel(scene);
        }
        finally
        {
            _loading = false;
        }
    }

    private void HandleLeaving(string scene)
    {
        if (!_loading)
        {
            SavedScene left = new();
            WriteEntities(left.Entities, left.Destroyed, withTravellers: false);
            _visited[scene] = left;
            _carried = [];
            foreach ((Entity entity, string id) in FindSaveables(Travellers))
            {
                _carried[id] = WriteEntity(entity);
            }
        }

        _started.Clear();
    }

    private void HandleArriving(string scene)
    {
        if (_loading)
        {
            return;
        }

        if (_visited.Remove(scene, out SavedScene? left))
        {
            ReadEntities(left.Entities, left.Destroyed);
        }

        ReadEntities(_carried, []);
        _carried = [];
    }

    /// <summary>Writes the state of the saveable entities by id, and the ids of those started here and gone since, into the collections.</summary>
    private void WriteEntities(Dictionary<string, Dictionary<string, JsonNode>> entities, List<string> destroyed, bool withTravellers)
    {
        HashSet<string> alive = [];
        foreach ((Entity entity, string id) in FindSaveables(Saveables))
        {
            alive.Add(id);
            if (!withTravellers && _world.Has<Traveller>(entity))
            {
                continue;
            }

            entities[id] = WriteEntity(entity);
        }

        foreach (string id in _started)
        {
            if (!alive.Contains(id))
            {
                destroyed.Add(id);
            }
        }
    }

    private Dictionary<string, JsonNode> WriteEntity(Entity entity)
    {
        Dictionary<string, JsonNode> states = [];
        foreach (SaveState state in _states.States)
        {
            JsonNode? node = state.Write(entity);
            if (node is not null)
            {
                states[state.Key] = node;
            }
        }

        return states;
    }

    private void WriteGlobals(SaveGame save)
    {
        foreach (GlobalSaveState state in _states.Globals)
        {
            JsonNode? node = state.Write();
            if (node is not null)
            {
                save.States[state.Key] = node;
            }
        }
    }

    /// <summary>Destroys the saveable entities listed as gone and reads the others' states back, by id.</summary>
    private void ReadEntities(Dictionary<string, Dictionary<string, JsonNode>> entities, IEnumerable<string> gone)
    {
        HashSet<string> destroyed = [.. gone];
        foreach ((Entity entity, string id) in FindSaveables(Saveables))
        {
            if (destroyed.Contains(id))
            {
                _lifecycle.Destroy(_world, entity);
                continue;
            }

            if (!entities.TryGetValue(id, out Dictionary<string, JsonNode>? states))
            {
                continue;
            }

            foreach (SaveState state in _states.States)
            {
                states.TryGetValue(state.Key, out JsonNode? node);
                state.Read(entity, node);
            }
        }
    }

    private void ReadGlobals(SaveGame save)
    {
        foreach (GlobalSaveState state in _states.Globals)
        {
            save.States.TryGetValue(state.Key, out JsonNode? node);
            state.Read(node);
        }
    }

    /// <summary>The saveable entities the query finds with an id.</summary>
    private List<(Entity Entity, string Id)> FindSaveables(in QueryDescription query)
    {
        List<(Entity, string)> found = [];
        _world.Query(in query, (Entity entity, ref Saveable saveable) =>
        {
            if (saveable.Id.Length > 0)
            {
                found.Add((entity, saveable.Id));
            }
        });
        return found;
    }

    private SaveGame? Read(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            return JsonSerializer.Deserialize(bytes, SavesJsonContext.Default.SaveGame);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            LogUnreadable(path, exception.Message);
            return null;
        }
    }

    /// <summary>The slot's file; false for a name that isn't a plain file name, so a slot can't leave the folder.</summary>
    private bool TryGetPath(string slot, out string path)
    {
        path = "";
        if (slot.Length == 0 || slot != Path.GetFileName(slot) || slot.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || slot.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        path = Path.Combine(_settings.Folder, slot + Extension);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The save {Path} can't be read: {Reason}")]
    private partial void LogUnreadable(string path, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The save {Slot} is from version {Version}, newer than this game's.")]
    private partial void LogNewer(string slot, int version);
}
