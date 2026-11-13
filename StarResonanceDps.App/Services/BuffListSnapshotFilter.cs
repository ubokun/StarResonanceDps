// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Config;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

/// <summary>
/// バフ・デバフ一覧に出す行を、一覧の設定で絞って並べる。プレイヤーから開いた一覧とエンティティリストから開いた一覧、
/// エンティティ情報のバフ・デバフの枠が使う。絞るのは非表示リスト(右クリックで足したバフ)、並べるのは優先表示リスト
/// (オンの行のバフを先頭からリストの並びで出し、残りは元の並びのまま。非表示リストに入っているバフは出さない)。
/// </summary>
public static class BuffListSnapshotFilter
{
    /// <summary>
    /// プレイヤーのバフ(デバフ)一覧の行。一覧の窓と同じ作り方(設定の「同種バフの表示」で取り、非表示リストで絞り、優先表示リストで並べる)。
    /// 一覧の窓・メイン画面のカードの選択リスト・バフ・デバフカードの「指定なし」が同じ行を見るように、ここ1か所で作る。
    /// </summary>
    public static IReadOnlyList<PlayerBuffSnapshot> GetPlayerRows(
        BuffListWidgetSettingsConfig settings,
        long characterId,
        PlayerBuffListKind kind)
    {
        return Apply(
            settings,
            MeterSnapshotProvider.GetPlayerBuffs(
                characterId,
                kind,
                WidgetConfigDefaults.GroupsSameBuffs(settings, isEntityTarget: false)));
    }

    /// <summary>落とす行が無く並びも変わらなければ、受け取った一覧をそのまま返す(新しい一覧を作らない)。</summary>
    public static IReadOnlyList<PlayerBuffSnapshot> Apply(
        BuffListWidgetSettingsConfig settings,
        IReadOnlyList<PlayerBuffSnapshot> snapshots)
    {
        return Apply(settings.HiddenBuffIds, settings.PriorityBuffs, snapshots);
    }

    /// <summary>エンティティ情報の枠。扱いはバフ・デバフ一覧と同じ。</summary>
    public static IReadOnlyList<PlayerBuffSnapshot> Apply(
        EntityInfoWidgetSettingsConfig settings,
        IReadOnlyList<PlayerBuffSnapshot> snapshots)
    {
        return Apply(settings.HiddenBuffIds, settings.PriorityBuffs, snapshots);
    }

    /// <param name="hiddenBuffIds">非表示リストのバフのID。</param>
    /// <param name="priorityBuffs">優先表示リストの行。オンでバフの番号の行だけを見る。</param>
    /// <returns>落とす行が無く並びも変わらなければ、受け取った一覧をそのまま返す(新しい一覧を作らない)。</returns>
    private static IReadOnlyList<PlayerBuffSnapshot> Apply(
        IReadOnlyCollection<int> hiddenBuffIds,
        IReadOnlyList<BuffDebuffGroupEntryConfig>? priorityBuffs,
        IReadOnlyList<PlayerBuffSnapshot> snapshots)
    {
        var dropsAny = false;
        var reorders = false;
        var previousRank = int.MinValue;
        for (var index = 0; index < snapshots.Count; index++)
        {
            var snapshot = snapshots[index];
            if (!IsShown(hiddenBuffIds, snapshot))
            {
                dropsAny = true;
                continue;
            }

            // 出す行の順位が元の並びのまま増えていくなら、並べ直しても同じ並びになる。
            var rank = GetPriorityRank(priorityBuffs, snapshot.BaseId);
            reorders |= rank < previousRank;
            previousRank = rank;
        }

        if (!dropsAny && !reorders)
        {
            return snapshots;
        }

        var shown = snapshots.Where(snapshot => IsShown(hiddenBuffIds, snapshot));

        // OrderBy は同じ順位の中で元の並びを保つ(優先表示でない行と、まとめない設定で並ぶ同じバフの行)。
        return reorders
            ? [.. shown.OrderBy(snapshot => GetPriorityRank(priorityBuffs, snapshot.BaseId))]
            : [.. shown];
    }

    private static bool IsShown(IReadOnlyCollection<int> hiddenBuffIds, PlayerBuffSnapshot snapshot)
        => !hiddenBuffIds.Contains(snapshot.BaseId);

    /// <summary>優先表示リストの並びでの位置。リストに無い・オフの行のバフは一番後ろ(<see cref="int.MaxValue"/>)。</summary>
    private static int GetPriorityRank(IReadOnlyList<BuffDebuffGroupEntryConfig>? priorityBuffs, int baseId)
    {
        if (priorityBuffs is null)
        {
            return int.MaxValue;
        }

        for (var index = 0; index < priorityBuffs.Count; index++)
        {
            var entry = priorityBuffs[index];
            if (entry.IsEnabled && entry.Group == BuffGroup.None && entry.BuffId == baseId)
            {
                return index;
            }
        }

        return int.MaxValue;
    }
}
