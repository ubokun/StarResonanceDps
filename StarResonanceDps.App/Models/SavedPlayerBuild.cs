namespace StarResonanceDps.App.Models;

public sealed class SavedPlayerBuild
{
    public int SchemaVersion { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public long CharacterId { get; set; }
    public string Server { get; set; } = "Unknown";
    public string PlayerName { get; set; } = "";
    public DateTime CapturedUtc { get; set; }
    public string Fingerprint { get; set; } = "";
    public string Details { get; set; } = "";
    public string ObservedTimeline { get; set; } = "";
    public string SourceData { get; set; } = "";
    public List<SavedBuildEncounter> Encounters { get; set; } = [];
    public string Label => $"{CapturedUtc.ToLocalTime():yyyy/MM/dd HH:mm:ss}";
    public string UserLabel => $"{PlayerName} ({Server} / {CharacterId})";
}

public sealed class SavedBuildEncounter
{
    public ulong EncounterId { get; set; }
    public DateTime StartTime { get; set; }
    public string SceneName { get; set; } = "";
    public string Timeline { get; set; } = "";
    public DateTime CapturedUtc { get; set; }
    public string Label => $"{StartTime:yyyy/MM/dd HH:mm:ss} · {SceneName} · #{EncounterId}（取得 {CapturedUtc.ToLocalTime():HH:mm:ss}）";
}
