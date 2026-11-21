using System.Text.Json;

using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>
/// Writes the game to save slots and reads it back: the facts, which hold quests, knowledge and standing, and whatever modules
/// write when <see cref="Writing"/> is raised and read when <see cref="Loaded"/> is. Each slot is a <c>.fvsave</c> JSON file in
/// the settings' folder.
/// </summary>
public sealed partial class SaveSystem
{
    public const string Extension = ".fvsave";

    private readonly IFactDatabase _facts;
    private readonly SaveSettings _settings;
    private readonly ILogger<SaveSystem> _logger;
    private readonly HashSet<Tag> _blocks = [];
    private TimeSpan _total;
    private TimeSpan _totalAtLoad;
    private TimeSpan _loadedPlayTime;

    public SaveSystem(Game game, SaveSettings settings)
    {
        _facts = game.Facts;
        _settings = settings;
        _logger = game.LoggerFactory.CreateLogger<SaveSystem>();
        game.Loop.Update += time => _total = time.Total;
    }

    /// <summary>Raised as a save is written, after the facts, so modules add their state.</summary>
    public event Action<SaveGame>? Writing;

    /// <summary>Raised after a save is read and the facts are back, so modules restore their state.</summary>
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

        Dictionary<Tag, int> facts = [];
        foreach ((string name, int value) in save.Facts)
        {
            Tag tag = new(name);
            facts[tag] = value;
        }

        _facts.RestoreFacts(facts);
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

        Writing?.Invoke(save);
        Directory.CreateDirectory(_settings.Folder);
        string temporary = path + ".tmp";
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(save, SavesJsonContext.Default.SaveGame);
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path, overwrite: true);
        return true;
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
