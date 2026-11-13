// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Globalization;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 潜在心相晶情報・モジュール情報。プレイヤーに乗っている根のバフ(名簿の行の <see cref="PlayerRosterEntry.RootBuffData"/>)のうち、
/// このウィジェットの種類のものを、種類の順・表の順で1行ずつ出す。根のバフの一覧をまだ受けていなければ何も出さない。
/// 自分のライブの行のモジュール効果だけは、根のバフ(段階5・6 の一部の効果だけ)ではなく、装着中のモジュール(<see cref="SelfModuleStore"/>)の
/// 全部の効果を段階つきで出す。因子も、根のバフ(バフを付ける因子だけ)ではなく、心相晶の育成データ(<see cref="SelfSeasonCultivateStore"/>)の
/// 付けている因子を全部出す。
///
/// <para>
/// 名簿の行は HP などで頻繁に入れ替わるので、行の中身(名前・絵)を作り直して前の中身と値で比べ、違う行だけ差し替える。
/// 根のバフは時間では変わらないのでタイマーは持たない。
/// 相手が一覧に居なくても分かっている値(<c>PlayerDirectory</c>。一覧から外れた人は最後の値)で出す(プレイヤー情報と同じ)。
/// </para>
/// </summary>
public sealed class PlayerRootBuffWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    /// <summary>リンク数の届いていないモジュール効果の段階で、書式の数字の位置へ差し込む文字(バフリストの経過不明と同じ)。</summary>
    private const string UnknownModuleLevelText = "??";

    private readonly ObservableCollection<PlayerRootBuffEntry> _entries = [];

    /// <summary>出す根のバフの種類。並びは行の並び。</summary>
    private readonly RootBuffKind[] _kinds;

    private PlayerRosterEntry? _player;
    private bool _isDisposed;

    public PlayerRootBuffWidgetViewModel(
        WidgetListItemViewModel widget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer,
        IReadOnlyList<RootBuffKind> kinds)
        : base(widget, requestedCharacterId)
    {
        _kinds = [.. kinds];
        Entries = new ReadOnlyObservableCollection<PlayerRootBuffEntry>(_entries);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        InitializePlayer(initialPlayer);
    }

    public ReadOnlyObservableCollection<PlayerRootBuffEntry> Entries { get; }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    /// <summary>相手が一覧に居なくても分かっている値で出す(プレイヤー情報と同じ)。</summary>
    protected override bool ShowsKnownPlayerOffList => true;

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        _player = player;
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    /// <summary>
    /// 自分のモジュール(<see cref="SelfModuleStore"/>)が変わった。自分を出しているモジュール情報の窓だけ作り直す
    /// (付け替えは名簿の行を変えないので、名簿の更新では作り直されない)。
    /// </summary>
    public void NotifySelfModulesChanged()
    {
        if (UsesSelfData(_player, RootBuffKind.ModuleEffect))
        {
            Refresh();
        }
    }

    /// <summary>
    /// 自分の心相晶の育成データ(<see cref="SelfSeasonCultivateStore"/>)が変わった。自分を出している潜在心相晶情報の窓だけ作り直す
    /// (因子の付け替えは名簿の行を変えないので、名簿の更新では作り直されない)。
    /// </summary>
    public void NotifySelfSeasonCultivateChanged()
    {
        if (UsesSelfData(_player, RootBuffKind.Factor))
        {
            Refresh();
        }
    }

    /// <summary>上級の絵は型で決まるので、上級の行を出しているときだけ型が分からないことを数える。</summary>
    protected override WidgetDataStatus CreateDataStatus(GameReceptionSnapshot reception, PlayerRosterEntry? player)
        => WidgetDataStatusRules.ForRootBuffs(
            reception,
            EncounterHistoryProvider.HasLoadFailed,
            IsSelfTarget,
            player,
            countsAdvancedType: _entries.Any(entry => entry.Kind == RootBuffKind.SeasonTalentAdvanced));

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        var entries = CreateEntries(_player);

        // 位置ごとに当て直し、増えたぶんだけ足し、減ったぶんだけ外す(中身が同じ行は差し替えない)。
        for (var index = 0; index < entries.Count; index++)
        {
            if (index < _entries.Count)
            {
                if (!Equals(_entries[index], entries[index]))
                {
                    _entries[index] = entries[index];
                }
            }
            else
            {
                _entries.Add(entries[index]);
            }
        }

        while (_entries.Count > entries.Count)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }
    }

    private List<PlayerRootBuffEntry> CreateEntries(PlayerRosterEntry? player)
    {
        var selfModuleEffects = player is { IsFromRecord: true }
            ? player.RecordedBuild?.ModuleEffects
            : UsesSelfData(player, RootBuffKind.ModuleEffect) ? SelfModuleStore.GetEquippedEffectLinks() : null;
        var selfFactors = player is { IsFromRecord: true }
            ? player.RecordedBuild?.Factors
            : UsesSelfData(player, RootBuffKind.Factor) ? GetSelfFactors() : null;
        var rootBuffData = player?.RootBuffData;
        var entries = new List<PlayerRootBuffEntry>();
        foreach (var kind in _kinds)
        {
            if (kind == RootBuffKind.ModuleEffect && selfModuleEffects is not null)
            {
                AddSelfModuleEntries(entries, selfModuleEffects);
            }
            else if (kind == RootBuffKind.Factor && selfFactors is not null)
            {
                AddSelfFactorEntries(entries, selfFactors);
            }
            else if (rootBuffData is not null)
            {
                AddRootBuffEntries(entries, rootBuffData, kind, player!.SeasonTalentBuffId);
            }
        }

        return entries;
    }

    /// <summary>
    /// その種類の行を、根のバフではなく自分の控え(モジュールは <see cref="SelfModuleStore"/>、因子は <see cref="SelfSeasonCultivateStore"/>)から作るか。
    /// 自分のライブの行だけ(履歴の回の記録から来た行は、その回の根のバフで出す)。控えがそろう前(全体コンテナの前)は根のバフで出す。
    /// </summary>
    private bool UsesSelfData(PlayerRosterEntry? player, RootBuffKind kind)
        => player is { IsSelf: true, IsFromRecord: false } && Array.IndexOf(_kinds, kind) >= 0;

    /// <summary>今のシーズンの潜在心相晶の、有効な型に付けている因子。今のシーズンか控えがまだなら null(根のバフで出す)。</summary>
    private static IReadOnlyList<SelfSeasonFactor>? GetSelfFactors()
    {
        var seasonId = SeasonStateStore.CurrentSeasonId;
        return seasonId > 0 ? SelfSeasonCultivateStore.GetActiveFactors(seasonId) : null;
    }

    /// <summary>根のバフのうち、その種類のものを表の順で足す。</summary>
    private static void AddRootBuffEntries(
        List<PlayerRootBuffEntry> entries,
        PlayerRootBuffData rootBuffData,
        RootBuffKind kind,
        int seasonTalentBuffId)
    {
        var rows = new List<(int Order, int BuffId)>();
        foreach (var buffId in rootBuffData.BuffIds)
        {
            if (CombatDataCatalog.TryGetRootBuffKind(buffId, out var buffKind, out var order) && buffKind == kind)
            {
                rows.Add((order, buffId));
            }
        }

        rows.Sort((left, right) => left.Order.CompareTo(right.Order));
        foreach (var (_, buffId) in rows)
        {
            var iconName = CombatDataCatalog.GetRootBuffIconName(buffId, seasonTalentBuffId);
            entries.Add(new PlayerRootBuffEntry(
                CombatDataCatalog.GetRootBuffName(buffId),
                CombatIconResolver.ResolveRootBuffIcon(kind, iconName),
                kind));
        }
    }

    /// <summary>
    /// 自分の装着中のモジュールの効果を、段階の高い順(同じ段階は効果の番号の順、段階の分からない効果は最後)に段階つきで足す。
    /// 段階はリンク数の和で決め、段階0 の効果(発動していない)は出さない。リンク数が届いていない効果は段階を「Lv??」にする。
    /// </summary>
    private static void AddSelfModuleEntries(List<PlayerRootBuffEntry> entries, IReadOnlyList<SelfModuleEffect> effects)
    {
        var rows = effects
            .Select(effect => (effect.EffectId, Level: effect.LinkCount is { } linkCount
                ? CombatDataCatalog.GetModuleEffectLevel(effect.EffectId, linkCount)
                : null))
            .Where(row => row.Level != 0)
            .OrderByDescending(row => row.Level ?? -1)
            .ThenBy(row => row.EffectId);
        foreach (var (effectId, level) in rows)
        {
            entries.Add(new PlayerRootBuffEntry(
                CombatDataCatalog.GetModuleEffectName(effectId),
                CombatIconResolver.ResolveRootBuffIcon(RootBuffKind.ModuleEffect, CombatDataCatalog.GetModuleEffectIconName(effectId)),
                RootBuffKind.ModuleEffect,
                LocalizationManager.Instance.Format(
                    "PlayerModule_LevelFormat",
                    level?.ToString(CultureInfo.InvariantCulture) ?? UnknownModuleLevelText)));
        }
    }

    /// <summary>
    /// 自分の付けている因子を、枠の番号の順で足す。名前は等級つきのアイテム名、絵は因子のアイテムの絵。
    /// 因子のアイテムの表(付けるバフ・絵)はサーバーで違うので、今つないでいるサーバーの表で引く。
    /// </summary>
    private static void AddSelfFactorEntries(List<PlayerRootBuffEntry> entries, IReadOnlyList<SelfSeasonFactor> factors)
    {
        var server = GameServerStore.Current;
        foreach (var factor in factors)
        {
            entries.Add(new PlayerRootBuffEntry(
                CombatDataCatalog.GetFactorItemName(server, factor.ItemId),
                CombatIconResolver.ResolveRootBuffIcon(RootBuffKind.Factor, CombatDataCatalog.GetFactorItemIconName(server, factor.ItemId)),
                RootBuffKind.Factor));
        }
    }
}
