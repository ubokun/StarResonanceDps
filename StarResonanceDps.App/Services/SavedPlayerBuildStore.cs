using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using StarResonanceDps.App.Models;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

public sealed class SavedPlayerBuildStore
{
    private readonly string _directory;
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true,
        IgnoreReadOnlyProperties = true, NumberHandling = JsonNumberHandling.Strict };
    public SavedPlayerBuildStore(string? directory = null) => _directory = directory
        ?? Path.Combine(CombatRuntimePaths.DataDirectory, "PlayerBuilds");

    public List<SavedPlayerBuild> Load(out int failed)
    {
        lock (Gate)
        {
            failed = 0;
            var results = new List<SavedPlayerBuild>();
            if (!Directory.Exists(_directory)) return results;
            foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
            {
                try
                {
                    var item = JsonSerializer.Deserialize<SavedPlayerBuild>(File.ReadAllText(path));
                    if (item is null || item.SchemaVersion != 1 || item.CharacterId <= 0
                        || item.Id == Guid.Empty || item.Server is null || item.Fingerprint is null
                        || item.Details is null || item.PlayerName is null || item.Encounters is null
                        || item.Encounters.Any(x => x is null || x.EncounterId == 0)
                        || Path.GetFileNameWithoutExtension(path) != item.Id.ToString("N"))
                        throw new InvalidDataException();
                    results.Add(item);
                }
                catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
                { failed++; }
            }
            return results.OrderByDescending(x => x.CapturedUtc).ToList();
        }
    }

    public SavedPlayerBuild Save(SavedPlayerBuild item)
    {
        lock (Gate)
        {
            if (item.CharacterId <= 0 || item.Encounters.Any(x => x.EncounterId == 0))
                throw new InvalidDataException("プレイヤー情報または紐づける集計が不正です。");
            var existing = Load(out _).FirstOrDefault(x => x.CharacterId == item.CharacterId
                && x.Server == item.Server && x.Fingerprint == item.Fingerprint);
            if (existing is not null)
            {
                foreach (var link in item.Encounters)
                {
                    // Preserve different observations of a growing live encounter with their capture times.
                    if (!existing.Encounters.Any(x => x.EncounterId == link.EncounterId && x.Timeline == link.Timeline))
                        existing.Encounters.Add(link);
                }
                existing.ObservedTimeline = item.ObservedTimeline;
                existing.CapturedUtc = item.CapturedUtc;
                item = existing;
            }
            Directory.CreateDirectory(_directory);
            var destination = Path.Combine(_directory, item.Id.ToString("N") + ".json");
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(item, Options));
                File.Move(temporary, destination, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return item;
        }
    }
}
