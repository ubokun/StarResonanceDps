using Newtonsoft.Json.Linq;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.CombatRuntime.DataTypes.Skills;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>Last observed build at encounter save time. Null collections mean unreceived, empty means known empty.</summary>
public sealed class RecordedPlayerBuildSnapshot
{
    public DateTime SavedUtc { get; set; }
    public int ProfessionId { get; set; }
    public int SubProfessionId { get; set; }
    public PlayerClassSpec ClassSpec { get; set; }
    public int SeasonTalentBuffId { get; set; }
    public bool IsSeasonTalentInactive { get; set; }
    public int[]? RootBuffIds { get; set; }
    public int[]? TalentNodeIds { get; set; }
    public Dictionary<int, int>? LastReplaceableSkills { get; set; }
    public PlayerEquipmentDataState? EquipmentState { get; set; }
    public PlayerEquipmentItem[]? EquipmentItems { get; set; }
    public List<SkillLevelInfo>? SkillLevels { get; set; }
    public List<SelfModuleEffect>? ModuleEffects { get; set; }
    public List<SelfSeasonFactor>? Factors { get; set; }

    internal static RecordedPlayerBuildSnapshot Capture(Entity entity, PlayerRosterEntry? displayed = null)
    {
        var id = entity.UID != 0 ? entity.UID : Utils.UuidToEntityId(entity.UUID);
        var source = PlayerDataSourceResolver.Resolve(id, null, entity, false, recordedOnly: true);
        var equipment = displayed?.EquipmentData ?? source.EquipmentData;
        var roots = displayed?.RootBuffData ?? source.RootBuffData;
        var nodes = displayed?.TalentNodeIds ?? source.TalentNodeIds;
        var replacements = displayed?.LastReplaceableSkills ?? source.LastReplaceableSkills;
        var levels = entity.GetAttrKV("AttrSkillLevelIdList") switch
        {
            IEnumerable<SkillLevelInfo> list => list,
            JArray array => array.ToObject<List<SkillLevelInfo>>(),
            _ => null
        };
        return new()
        {
            SavedUtc = DateTime.UtcNow,
            ProfessionId = displayed?.ProfessionId ?? source.ProfessionId,
            SubProfessionId = displayed?.SubProfessionId ?? source.SubProfessionId,
            ClassSpec = displayed?.ClassSpec ?? source.ClassSpec,
            SeasonTalentBuffId = displayed?.SeasonTalentBuffId ?? source.SeasonTalentBuffId,
            IsSeasonTalentInactive = displayed?.IsSeasonTalentInactive ?? source.IsSeasonTalentInactive,
            RootBuffIds = roots?.BuffIds.ToArray(), TalentNodeIds = nodes?.ToArray(),
            LastReplaceableSkills = replacements?.ToDictionary(x => x.Key, x => x.Value),
            EquipmentState = equipment?.State, EquipmentItems = equipment?.Items.ToArray(),
            SkillLevels = levels?.Select(x => new SkillLevelInfo { SkillId=x.SkillId, CurrentLevel=x.CurrentLevel, Tier=x.Tier, Icon=x.Icon }).ToList(),
            ModuleEffects = displayed?.IsSelf == true ? SelfModuleStore.GetEquippedEffectLinks()?.ToList() : null,
            Factors = displayed?.IsSelf == true && SeasonStateStore.CurrentSeasonId > 0
                ? SelfSeasonCultivateStore.GetActiveFactors(SeasonStateStore.CurrentSeasonId)?.ToList() : null
        };
    }

    internal PlayerDataSourceSnapshot ApplyTo(PlayerDataSourceSnapshot source) => source with
    {
        ProfessionId = ProfessionId, SubProfessionId = SubProfessionId, ClassSpec = ClassSpec,
        SeasonTalentBuffId = SeasonTalentBuffId, IsSeasonTalentInactive = IsSeasonTalentInactive,
        RootBuffData = RootBuffIds is null ? null : PlayerRootBuffData.FromSorted(RootBuffIds),
        TalentNodeIds = TalentNodeIds, LastReplaceableSkills = LastReplaceableSkills,
        EquipmentData = EquipmentState switch
        {
            PlayerEquipmentDataState.Available => PlayerEquipmentData.Create(EquipmentItems ?? []),
            PlayerEquipmentDataState.Invalid => PlayerEquipmentData.Invalid,
            _ => null
        }
    };
}
