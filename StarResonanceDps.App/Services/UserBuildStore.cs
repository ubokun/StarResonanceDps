using System.IO;
using System.Text.Json;
using StarResonanceDps.App.Models;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

public sealed class UserBuildStore
{
    private readonly string _directory;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public UserBuildStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(CombatRuntimePaths.DataDirectory, "UserBuilds");
    }

    public IReadOnlyList<UserBuild> Load(out int failedCount)
    {
        failedCount = 0;
        if (!Directory.Exists(_directory)) return [];
        var builds = new List<UserBuild>();
        var ids = new HashSet<Guid>();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                var build = JsonSerializer.Deserialize<UserBuild>(File.ReadAllText(path));
                if (build is null || build.SchemaVersion != 1 || build.Id == Guid.Empty
                    || Path.GetFileNameWithoutExtension(path) != build.Id.ToString("N")
                    || string.IsNullOrWhiteSpace(build.Name) || !ids.Add(build.Id))
                    throw new InvalidDataException("Invalid build file.");
                build.Mindscape ??= "";
                build.Abilities ??= "";
                build.Imagines ??= "";
                build.SkillTimeline ??= "";
                builds.Add(build);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                failedCount++;
            }
        }
        return builds.OrderByDescending(x => x.UpdatedUtc).ToList();
    }

    public void Save(UserBuild build)
    {
        if (string.IsNullOrWhiteSpace(build.Name) || build.Id == Guid.Empty || build.SchemaVersion != 1)
            throw new InvalidDataException("ビルド名を入力してください。");
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, build.Id.ToString("N") + ".json");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        build.UpdatedUtc = DateTime.UtcNow;
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(build, Options));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
