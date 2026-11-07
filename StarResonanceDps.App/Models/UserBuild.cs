namespace StarResonanceDps.App.Models;

public sealed class UserBuild
{
    public int SchemaVersion { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Mindscape { get; set; } = "";
    public string Abilities { get; set; } = "";
    public string Imagines { get; set; } = "";
    public string SkillTimeline { get; set; } = "";
    public DateTime UpdatedUtc { get; set; }
}
