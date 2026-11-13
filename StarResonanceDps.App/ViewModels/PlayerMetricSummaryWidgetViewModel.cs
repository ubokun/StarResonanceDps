// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class PlayerMetricSummaryWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    private readonly PlayerMetricSummaryKind _kind;
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly DispatcherTimer _refreshTimer;
    private readonly Dictionary<Zproto.EDamageProperty, Brush> _elementBrushes = [];
    private readonly Dictionary<Zproto.EDamageMode, Brush> _modeBrushes = [];
    private readonly Dictionary<Zproto.EDamageProperty, PlayerMetricShareItem> _elementShareItems = [];
    private readonly Dictionary<Zproto.EDamageMode, PlayerMetricShareItem> _modeShareItems = [];
    private readonly List<PlayerMetricShareItem> _orderedShareItems = [];
    private ElementColorWidgetSettingsConfig _elementColorSettings;
    private bool _isDisposed;

    /// <summary>表示する中身。窓を開いたときに作り、更新では文字・色・バーの長さと割合の行の並びだけを変える。</summary>
    public PlayerMetricSummaryEntry Summary { get; } = new();

    public PlayerMetricSummaryWidgetViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer,
        PlayerMetricSummaryKind kind)
        : base(playerWidget, requestedCharacterId)
    {
        _kind = kind;
        _elementColorSettings = playerWidget.GetElementColorSettingsSnapshot();
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        playerWidget.ElementColorSettingsChanged += Widget_ElementColorSettingsChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        InitializePlayer(initialPlayer);
        Refresh();
        _refreshTimer.Start();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _configManager.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        PlayerWidget.ElementColorSettingsChanged -= Widget_ElementColorSettingsChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        Refresh();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
        // 相手の素性・パーティの名簿は名簿の行を変えずに届くことがあるので、読み直すたびに見る。
        RefreshDataStatus();
    }

    /// <summary>料理と自然回復はヒール側だけが数える。技の段階は出さない。</summary>
    protected override WidgetDataStatus CreateDataStatus(GameReceptionSnapshot reception, PlayerRosterEntry? player)
        => WidgetDataStatusRules.ForPlayerMetric(
            reception,
            EncounterHistoryProvider.SelectedEncounterId is not null,
            EncounterHistoryProvider.HasLoadFailed,
            IsSelfTarget,
            SelectedCharacterId is { } characterId ? MeterSnapshotProvider.GetPlayerIdentity(characterId) : null,
            PartyStateStore.Instance.Current.IsMembershipPending,
            countsSkillLevels: false,
            countsHealingSources: _kind == PlayerMetricSummaryKind.Healing);

    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    /// <summary>アイコンカラーの設定(保存かプレビュー)が変わった。属性と物理・魔法・無分類のバーの色を作り直す。</summary>
    private void Widget_ElementColorSettingsChanged(object? sender, EventArgs e)
    {
        _elementColorSettings = PlayerWidget.GetElementColorSettingsSnapshot();
        _elementBrushes.Clear();
        _modeBrushes.Clear();
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        var numberDisplayFormatIndex = _configManager.CurrentSettings.NumberDisplayFormatIndex;
        if (SelectedCharacterId is not { } characterId)
        {
            ApplySnapshot(MeterSnapshotProvider.GetPlayerMetricSummary(_kind, 0), numberDisplayFormatIndex);
            return;
        }

        var playerIdentity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        if (playerIdentity is not null)
        {
            SetHeaderText(playerIdentity.Name, playerIdentity.UserId, playerIdentity.IsNpc, playerIdentity.ProfessionId);
        }

        ApplySnapshot(MeterSnapshotProvider.GetPlayerMetricSummary(_kind, characterId), numberDisplayFormatIndex);
    }

    /// <summary>表の文字と、割合の行を入れる。部品は作り直さない(同じ値なら何も起きない)。</summary>
    private void ApplySnapshot(PlayerMetricSummarySnapshot snapshot, int numberDisplayFormatIndex)
    {
        var summary = Summary;
        summary.TotalRow.ValueText = FormatValue(snapshot.TotalValue, numberDisplayFormatIndex);
        summary.TotalRow.HitsText = FormatHits(snapshot.HitsCount);

        summary.NormalRow.ValueText = FormatValue(snapshot.NormalValue, numberDisplayFormatIndex);
        summary.NormalRow.RateText = FormatPercent(snapshot.NormalRate);
        summary.NormalRow.HitsText = FormatHits(snapshot.NormalCount);

        summary.CritRow.ValueText = FormatValue(snapshot.CritValue, numberDisplayFormatIndex);
        summary.CritRow.RateText = FormatPercent(snapshot.CritRate);
        summary.CritRow.HitsText = FormatHits(snapshot.CritCount);

        summary.LuckyRow.ValueText = FormatValue(snapshot.LuckyValue, numberDisplayFormatIndex);
        summary.LuckyRow.RateText = FormatPercent(snapshot.LuckyRate);
        summary.LuckyRow.HitsText = FormatHits(snapshot.LuckyHitCount);

        ApplyElementShares(snapshot, numberDisplayFormatIndex);
        ApplyModeShares(snapshot, numberDisplayFormatIndex);
        summary.HasData = snapshot.TotalValue > 0UL;
    }

    /// <summary>属性の割合。バーの色はアイコンカラーの属性の行、アイコンは属性アイコン。</summary>
    private void ApplyElementShares(PlayerMetricSummarySnapshot snapshot, int numberDisplayFormatIndex)
    {
        ApplyShares(
            Summary.ElementShares,
            _elementShareItems,
            snapshot.TotalValue,
            snapshot.ValueByElement,
            numberDisplayFormatIndex,
            usesPlayerStatusAsset: false,
            GetElementBrush,
            ElementIconMasks.Get,
            static element => LocalizationManager.Instance.GetString($"DamageProperty_{element}"));
    }

    /// <summary>物理・魔法・無分類の割合。バーの色はアイコンカラーの物理・魔法・無分類の行、アイコンはステータス詳細の物理攻撃力・魔法攻撃力の絵(無分類ははてな)。</summary>
    private void ApplyModeShares(PlayerMetricSummarySnapshot snapshot, int numberDisplayFormatIndex)
    {
        ApplyShares(
            Summary.ModeShares,
            _modeShareItems,
            snapshot.TotalValue,
            snapshot.ValueByMode,
            numberDisplayFormatIndex,
            usesPlayerStatusAsset: true,
            GetModeBrush,
            DamageModeIconMasks.Get,
            GetModeName);
    }

    /// <summary>
    /// 割合の行を種類ごとに1つ作って使い回し(スキル詳細の行と同じ)、値のある種類を総量の大きい順に並べる。
    /// 行ごと並べ替えるので、順位が入れ替わってもバーはその種類の前の長さから伸び縮みする。
    /// 値の無くなった種類の行は一覧から外す(行は <paramref name="items"/> に残して、また値が入ったら使う)。
    /// </summary>
    private void ApplyShares<TKey>(
        ObservableCollection<PlayerMetricShareItem> shares,
        Dictionary<TKey, PlayerMetricShareItem> items,
        ulong totalValue,
        IReadOnlyList<KeyValuePair<TKey, ulong>> values,
        int numberDisplayFormatIndex,
        bool usesPlayerStatusAsset,
        Func<TKey, Brush> getBarBrush,
        Func<TKey, Brush?> getIconMask,
        Func<TKey, string> getName)
        where TKey : notnull
    {
        List<KeyValuePair<TKey, ulong>> ordered = totalValue == 0UL
            ? []
            : values.OrderByDescending(pair => pair.Value).ToList();
        // バーの長さは1位の値で割る(スキル詳細と同じ)。並びは総量の降順なので先頭が1位。
        var topValue = ordered.Count == 0 ? 0UL : ordered[0].Value;

        for (var index = 0; index < ordered.Count; index++)
        {
            var pair = ordered[index];
            if (!items.TryGetValue(pair.Key, out var item))
            {
                item = new PlayerMetricShareItem();
                items.Add(pair.Key, item);
            }

            item.BarBrush = getBarBrush(pair.Key);
            item.BarRatio = topValue == 0UL ? 0d : Math.Clamp(pair.Value / (double)topValue, 0d, 1d);
            item.IconMask = getIconMask(pair.Key);
            item.UsesPlayerStatusAsset = usesPlayerStatusAsset;
            item.Name = getName(pair.Key);
            item.ValueText = FormatValue(pair.Value, numberDisplayFormatIndex)
                + " "
                + FormatPercent(pair.Value / (double)totalValue * 100d);
            _orderedShareItems.Add(item);
        }

        CollectionOrder.Apply(shares, _orderedShareItems);
        _orderedShareItems.Clear();
    }

    private static string GetModeName(Zproto.EDamageMode mode)
    {
        return mode switch
        {
            Zproto.EDamageMode.DamagePhysical => LocalizationManager.Instance.GetString("Metric_DamageMode_Physical"),
            Zproto.EDamageMode.DamageMagical => LocalizationManager.Instance.GetString("Metric_DamageMode_Magical"),
            _ => LocalizationManager.Instance.GetString("Metric_DamageMode_Normal")
        };
    }

    /// <summary>属性のバーの塗り。設定が変われば <see cref="_elementBrushes"/> ごと捨てて作り直す。</summary>
    private Brush GetElementBrush(Zproto.EDamageProperty element)
    {
        if (!_elementBrushes.TryGetValue(element, out var brush))
        {
            brush = ElementBarColors.CreateBarBrush(
                _elementColorSettings,
                ElementBarColors.Resolve(_elementColorSettings, element));
            _elementBrushes[element] = brush;
        }

        return brush;
    }

    /// <summary>物理・魔法・無分類のバーの塗り。設定が変われば <see cref="_modeBrushes"/> ごと捨てて作り直す。</summary>
    private Brush GetModeBrush(Zproto.EDamageMode mode)
    {
        if (!_modeBrushes.TryGetValue(mode, out var brush))
        {
            brush = ElementBarColors.CreateBarBrush(
                _elementColorSettings,
                ElementBarColors.Resolve(_elementColorSettings, mode));
            _modeBrushes[mode] = brush;
        }

        return brush;
    }

    private static string FormatValue(double value, int numberDisplayFormatIndex)
    {
        return MeterNumberFormatter.Format(value, numberDisplayFormatIndex);
    }

    private static string FormatPercent(double value)
    {
        return value.ToString("F2", CultureInfo.CurrentCulture) + "%";
    }

    private static string FormatHits(ulong count)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            LocalizationManager.Instance.GetString("Metric_SummaryHitsFormat"),
            count);
    }
}
