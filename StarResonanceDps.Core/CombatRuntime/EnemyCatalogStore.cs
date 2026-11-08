using System.Text.Json;
using StarResonanceDps.Core.Services;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

public sealed record EnemyCatalogEntry
{
    public string Server { get; init; } = "";
    public long TypeId { get; init; }
    public bool Enabled { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public List<int> SkillIds { get; set; } = [];
    public List<ulong> Encounters { get; set; } = [];
    public string Key => $"{Server}:{TypeId}";
}

public sealed class EnemyCatalogStore
{
    public static EnemyCatalogStore Current { get; } = new(Path.Combine(CombatRuntimePaths.DataDirectory, "EnemyCatalog.json"));
    private readonly string _path;
    private readonly object _gate = new(), _saveGate = new();
    private readonly Dictionary<string, EnemyCatalogEntry> _entries = [];
    private long _revision, _savedRevision;
    private bool _loadFailed;
    public string? LastIssue { get; private set; }
    public EnemyCatalogStore(string path)
    {
        _path = path;
        try
        {
            if (!File.Exists(path)) return;
            var entries = JsonSerializer.Deserialize<List<EnemyCatalogEntry>>(File.ReadAllText(path)) ?? throw new InvalidDataException("一覧が空です。");
            foreach (var entry in entries)
            {
                if (entry.TypeId <= 0 || string.IsNullOrWhiteSpace(entry.Server) || entry.SkillIds is null || entry.Encounters is null)
                    throw new InvalidDataException("敵の識別情報が不正です。");
                _entries[entry.Key] = entry;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        { _entries.Clear(); _loadFailed = true; LastIssue = "敵一覧を読み込めませんでした。元ファイルを保護しています：" + ex.Message; }
    }
    public static string ServerFor(Encounter encounter) => ReferenceEquals(encounter, EncounterManager.Current)
        ? GameServerStore.Current.ToString() : encounter.ExData.GameServer.ToString();
    public static long TypeFor(Entity? entity) => entity?.EntityType == EEntityType.EntMonster && entity.GetAttrKV("AttrId") is { } value ? Convert.ToInt64(value) : 0;
    public void Observe(Encounter encounter, long uuid, DateTime utc, bool saved = false)
    {
        var entity = encounter.Entities.GetValueOrDefault(uuid);
        var typeId = TypeFor(entity);
        if (typeId <= 0 || _loadFailed) return;
        var server = saved ? encounter.ExData.GameServer.ToString() : ServerFor(encounter);
        var key = $"{server}:{typeId}";
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry))
                _entries[key] = entry = new() { Server = server, TypeId = typeId, FirstSeenUtc = utc, LastSeenUtc = utc };
            if (utc < entry.FirstSeenUtc) entry.FirstSeenUtc = utc;
            if (utc > entry.LastSeenUtc) entry.LastSeenUtc = utc;
            if (entity!.GetAttrKV("AttrSkillId") is int skill && skill > 0 && !entry.SkillIds.Contains(skill)) entry.SkillIds.Add(skill);
            foreach (var action in (saved ? encounter.ExData.EnemyControlEvents : []).Where(x => x.EntityUuid == uuid && x.SkillId > 0))
                if (!entry.SkillIds.Contains(action.SkillId)) entry.SkillIds.Add(action.SkillId);
            if (saved && !entry.Encounters.Contains(encounter.EncounterId))
            {
                entry.Encounters.Add(encounter.EncounterId);
                if (entry.Encounters.Count > 100) entry.Encounters.RemoveAt(0);
            }
            _revision++;
        }
    }
    public IReadOnlyList<EnemyCatalogEntry> Read()
    {
        lock (_gate) return _entries.Values.OrderByDescending(x => x.LastSeenUtc)
            .Select(x => x with { SkillIds = [..x.SkillIds], Encounters = [..x.Encounters] }).ToArray();
    }
    public bool IsEnabled(Encounter encounter, long uuid)
    {
        var key = $"{ServerFor(encounter)}:{TypeFor(encounter.Entities.GetValueOrDefault(uuid))}";
        lock (_gate) return _entries.TryGetValue(key, out var entry) && entry.Enabled;
    }
    public void SetEnabled(string key, bool value)
    {
        lock (_gate) { if (_entries.TryGetValue(key, out var entry)) { entry.Enabled = value; _revision++; } }
    }
    public void Save()
    {
        lock (_saveGate)
        {
            string json; long revision;
            lock (_gate)
            {
                if (_loadFailed || _revision == _savedRevision) return;
                revision = _revision; json = JsonSerializer.Serialize(_entries.Values, new JsonSerializerOptions { WriteIndented = true });
            }
            var temp = _path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
                File.WriteAllText(temp, json); File.Move(temp, _path, true);
                lock (_gate) _savedRevision = revision;
                LastIssue = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { LastIssue = "敵一覧を保存できませんでした：" + ex.Message; }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
