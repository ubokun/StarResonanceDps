using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using StarResonanceDps.Core.CombatRuntime.DataTypes.Skills;
using StarResonanceDps.App.Models;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

public static class PlayerBuildCapture
{
    public static Task<SavedPlayerBuild> CaptureAsync(long characterId, bool allowObserved = false)
    {
        var completion = new TaskCompletionSource<SavedPlayerBuild>(TaskCreationOptions.RunContinuationsAsynchronously);
        MessageManager.RunOnPacketThread(() =>
        {
            try { completion.SetResult(CaptureObserved(characterId, null, allowObserved)); }
            catch (Exception ex) { completion.SetException(ex); }
        });
        return completion.Task;
    }

    internal static SavedPlayerBuild Capture(long characterId, Encounter? specifiedEncounter = null) => CaptureObserved(characterId, specifiedEncounter, false);

    private static SavedPlayerBuild CaptureObserved(long characterId, Encounter? specifiedEncounter, bool allowObserved)
    {
        var source = MeterSnapshotProvider.GetBuildPlayer(characterId, specifiedEncounter ?? (allowObserved && AppState.OpenedHistoricalEncounter is null ? EncounterManager.Current : null))
            ?? throw new InvalidOperationException("表示中の集計にプレイヤー情報がありません。");
        var (encounter, player, entity) = source;
        // Only players with actual combat records can be linked, never nearby roster entries alone.
        var hasCombat = !(entity.TotalDamage == 0 && entity.TotalHealing == 0 && !entity.TakenStats.HasSkillSnapshots()
            && !entity.IsNamedInCombatEventLog);
        if (player.IsNpc || (!allowObserved && !hasCombat))
            throw new InvalidOperationException("このプレイヤーには集計の戦闘記録がありません。");
        var tree = PlayerTalentTreeWidgetViewModel.ResolveTree(player);
        var skillLevels = entity.GetAttrKV("AttrSkillLevelIdList") switch
        {
            List<SkillLevelInfo> list => list,
            JArray array => array.ToObject<List<SkillLevelInfo>>(),
            _ => null
        };
        var learnedImagines = skillLevels?.Where(x => CombatDataCatalog.IsSkillImagine(x.SkillId,
            CombatDataCatalog.GetSkillIconName(x.SkillId, x.Icon))).Select(x => new { x.SkillId, x.Tier, x.CurrentLevel })
            .OrderBy(x => x.SkillId).ToArray();
        var abilities = tree?.Nodes.Where(x => x.IsAbility || x.IsSpecRoot).Select(node => new
        {
            node.Id, node.TalentId,
            State = node.IsSpecRoot ? TalentTreeNodeKind.SpecRoot
                : PlayerTalentTreeWidgetViewModel.ResolveAbilityKind(node, player.RootBuffData!, player)
        }).ToArray();
        var raw = JsonSerializer.Serialize(new
        {
            player.ProfessionId, player.SubProfessionId, player.ClassSpec,
            player.SeasonTalentBuffId, player.IsSeasonTalentInactive,
            RootBuffIds = player.RootBuffData?.BuffIds,
            EquipmentState = player.EquipmentData?.State,
            Equipment = player.EquipmentData?.Items.OrderBy(x => x.Slot).ToArray(),
            Abilities = abilities,
            LearnedImagines = learnedImagines,
            TalentNodes = player.TalentNodeIds?.Order().ToArray(),
            Replacements = player.LastReplaceableSkills?.OrderBy(x => x.Key).ToArray()
        });
        var details = new StringBuilder();
        details.AppendLine($"{player.Name} / {player.GameServer} / {characterId}");
        details.AppendLine($"職業 {player.ProfessionId} / 特化 {player.ClassSpec}");
        details.AppendLine("取得元：表示中の集計に残された情報（最後に確認された構成）");
        details.AppendLine("\n【アビリティ】");
        if (abilities is null) details.AppendLine("未取得（ツリー・バフ情報が不足）");
        else foreach (var node in abilities)
            details.AppendLine($"{CombatDataCatalog.GetTalentName(node.TalentId)} [#{node.Id}]：{node.State switch
            { TalentTreeNodeKind.Taken => "取得", TalentTreeNodeKind.SpecRoot => "特化", TalentTreeNodeKind.Unknown => "不明", _ => "未取得（判定済み）" }}");
        details.AppendLine("\n【心相・因子などの確認済みバフ】");
        details.AppendLine(player.IsSeasonTalentInactive ? "心相の型：無効"
            : player.SeasonTalentBuffId > 0 ? "心相の型：" + CombatDataCatalog.GetSeasonTalentName(player.SeasonTalentBuffId) : "心相の型：未取得");
        if (player.RootBuffData is null) details.AppendLine("根バフ：未取得");
        else foreach (var id in player.RootBuffData.BuffIds)
            details.AppendLine($"{CombatDataCatalog.GetBuffName(id)} [#{id}]");
        details.AppendLine("\n【装備】");
        if (player.EquipmentData is null) details.AppendLine("未取得");
        else if (player.EquipmentData.State == StarResonanceDps.Core.Models.PlayerEquipmentDataState.Invalid)
            details.AppendLine("受信した装備情報を解析できませんでした。");
        else foreach (var equipment in player.EquipmentData.Items)
            details.AppendLine($"スロット {equipment.Slot}：装備 #{equipment.EquipmentId}");
        details.AppendLine("\n【確認できた習得イマジン（装備確定ではありません）】");
        if (learnedImagines is null) details.AppendLine("未取得");
        else if (learnedImagines.Length == 0) details.AppendLine("受信した一覧にイマジンなし");
        else foreach (var imagine in learnedImagines)
            details.AppendLine($"{CombatDataCatalog.GetSkillDisplayName(imagine.SkillId)} [#{imagine.SkillId}] 改造段階 {imagine.Tier} / Lv {imagine.CurrentLevel}");
        // Avoid the graph helper's live skill-grade caches when capturing historical records.
        var activations = encounter.GetSkillActivationsFrom(entity.UUID, 0, out _).Select(record =>
        {
            var (name, icon, frame, imagine) = CombatDataCatalog.GetSkillActivationDisplay(record.SkillId);
            return new MetricGraphSkillActivation(Math.Max(0, encounter.ToCombatOffset(record.Timestamp)?.TotalSeconds ?? 0),
                record.SkillId, icon, name, frame, imagine, null);
        }).ToArray();
        details.AppendLine($"\n【この取得時の集計 #{encounter.EncounterId} で使用を確認したイマジン】");
        var imagines = activations.Where(x => x.UsesImagineAsset).DistinctBy(x => x.SkillId).ToArray();
        if (imagines.Length == 0) details.AppendLine("使用記録なし（未装備とは限りません）");
        foreach (var imagine in imagines) details.AppendLine($"{imagine.Name} [#{imagine.SkillId}]");
        // Include observed imagine identity in the build key, without attributing it to every other encounter.
        raw = JsonSerializer.Serialize(new { Build = JsonSerializer.Deserialize<JsonElement>(raw),
            Imagines = learnedImagines is null ? imagines.Select(x => new { x.SkillId, x.Grade }).OrderBy(x => x.SkillId).ToArray() : null });
        var timeline = string.Join(Environment.NewLine, activations.Select(x =>
            $"{TimeSpan.FromSeconds(x.Seconds):hh\\:mm\\:ss\\.fff}  {x.Name} [#{x.SkillId}]"));
        return new SavedPlayerBuild
        {
            CharacterId = characterId, PlayerName = player.Name, Server = player.GameServer.ToString()!,
            CapturedUtc = DateTime.UtcNow, Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))),
            Details = details.ToString(), SourceData = raw,
            Encounters = !hasCombat ? [] : [new SavedBuildEncounter { EncounterId = encounter.EncounterId,
                StartTime = encounter.StartTime, SceneName = encounter.SceneName ?? "", Timeline = timeline, CapturedUtc = DateTime.UtcNow }]
        };
    }
}
