// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 装備詳細。部位 200〜210 ごとに1行(届いた一覧に無い部位は「不明」)。装備の一覧そのものが無いときは何も出さない。
///
/// <para>
/// 行は次のときにまとめて作り直す: 相手の装備・特化が変わった、今のシーズンが変わった、
/// 表示設定(書式・テキストカラー)が変わった、表示の言語が変わった、自分を出していて自分の装備の値が変わった。
/// 装備は時間では変わらないのでタイマーは持たない。
/// 相手が一覧に居なくても分かっている値(<c>PlayerDirectory</c>。一覧から外れた人は最後の値)で出す(プレイヤー情報と同じ)。
/// </para>
/// </summary>
public sealed partial class PlayerEquipmentWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    private readonly ObservableCollection<PlayerEquipmentSlotEntry> _equipmentSlots = [];
    private EquipmentWidgetSettingsConfig _settings;
    private PlayerRosterEntry? _player;

    /// <summary>行を作ったときのシーズン(<see cref="GetSeasonId"/>)。</summary>
    private int _builtSeasonId;

    private bool _isDisposed;

    [ObservableProperty]
    private PlayerEquipmentDataState _equipmentDataState = PlayerEquipmentDataState.Missing;

    public PlayerEquipmentWidgetViewModel(
        WidgetListItemViewModel equipmentWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
        : base(equipmentWidget, requestedCharacterId)
    {
        EquipmentSlots = new ReadOnlyObservableCollection<PlayerEquipmentSlotEntry>(_equipmentSlots);
        _settings = equipmentWidget.GetEquipmentSettingsSnapshot();
        equipmentWidget.EquipmentSettingsChanged += Widget_EquipmentSettingsChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        InitializePlayer(initialPlayer);
    }

    public ReadOnlyObservableCollection<PlayerEquipmentSlotEntry> EquipmentSlots { get; }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        PlayerWidget.EquipmentSettingsChanged -= Widget_EquipmentSettingsChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    /// <summary>相手が一覧に居なくても分かっている値で出す(プレイヤー情報と同じ)。</summary>
    protected override bool ShowsKnownPlayerOffList => true;

    /// <summary>
    /// ロスターの行は HP などで頻繁に入れ替わる。行に効く値(装備・特化)が変わったときだけ作り直す。
    /// </summary>
    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        var isSameSource = HasSameEquipmentSource(_player, player);
        _player = player;

        // 履歴の行とライブの行を行き来すると、装備が同じでもシーズンが変わる。
        if (!isSameSource || GetSeasonId() != _builtSeasonId)
        {
            Rebuild();
        }
    }

    /// <summary>シーズンでシーズン強度の有効・無効が変わるので、シーズンが変わったら作り直す。</summary>
    protected override void OnRosterContextChanged()
    {
        if (GetSeasonId() != _builtSeasonId)
        {
            Rebuild();
        }
    }

    /// <summary>
    /// 行のシーズン。素性が履歴の回から来た行はその回のシーズン、ライブの行は一覧の今のシーズン。
    /// </summary>
    private int GetSeasonId()
    {
        return _player?.SeasonId ?? RosterSeasonId;
    }

    /// <summary>
    /// 自分の装備の値(<c>SelfEquipmentStore</c>)が変わった。自分を出している窓だけ作り直す
    /// (改鋳・突破・特化の切り替えはロスターの装備ID を変えないので、ロスターの更新では作り直されない)。
    /// </summary>
    public void NotifySelfEquipmentChanged()
    {
        if (_player?.IsSelf == true)
        {
            Rebuild();
            RefreshDataStatus();
        }
    }

    protected override WidgetDataStatus CreateDataStatus(GameReceptionSnapshot reception, PlayerRosterEntry? player)
        => WidgetDataStatusRules.ForPlayerEquipment(
            reception,
            EncounterHistoryProvider.HasLoadFailed,
            IsSelfTarget,
            player,
            SelfEquipmentStore.HasState);

    /// <summary>表示設定(保存かプレビュー)が変わった。</summary>
    private void Widget_EquipmentSettingsChanged(object? sender, EventArgs e)
    {
        _settings = PlayerWidget.GetEquipmentSettingsSnapshot();
        Rebuild();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        Rebuild();
    }

    private void Rebuild()
    {
        if (_isDisposed)
        {
            return;
        }

        var equipmentData = _player?.EquipmentData;
        EquipmentDataState = equipmentData?.State ?? PlayerEquipmentDataState.Missing;
        _builtSeasonId = GetSeasonId();

        _equipmentSlots.Clear();
        if (_player is null || equipmentData?.State != PlayerEquipmentDataState.Available)
        {
            return;
        }

        foreach (var slot in PlayerEquipmentSlotEntry.CreateAll(
                     equipmentData,
                     _player,
                     _builtSeasonId,
                     _settings.InfoFormat,
                     CreateQualityBrushes(_settings)))
        {
            _equipmentSlots.Add(slot);
        }
    }

    /// <summary>設定のテキストカラーの各行の選択色を、品質の番号ごとの刷にする。</summary>
    private static IReadOnlyDictionary<int, Brush> CreateQualityBrushes(EquipmentWidgetSettingsConfig settings)
    {
        var brushes = new Dictionary<int, Brush>(WidgetConfigDefaults.EquipmentTextColorKeys.Length);
        foreach (var key in WidgetConfigDefaults.EquipmentTextColorKeys)
        {
            if (!settings.TextColorPalettes.TryGetValue(key, out var palette) || palette is not { Count: > 0 })
            {
                continue;
            }

            var index = settings.TextColorIndexes.TryGetValue(key, out var saved)
                ? saved
                : WidgetConfigDefaults.DefaultEquipmentTextColorIndex;

            if (!ColorUtilities.TryParseHex(palette[Math.Clamp(index, 0, palette.Count - 1)], out var color))
            {
                continue;
            }

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            brushes[int.Parse(key, CultureInfo.InvariantCulture)] = brush;
        }

        return brushes;
    }

    private static bool HasSameEquipmentSource(PlayerRosterEntry? left, PlayerRosterEntry? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return Equals(left.EquipmentData, right.EquipmentData)
            && left.SubProfessionId == right.SubProfessionId
            && left.ClassSpec == right.ClassSpec
            && left.ProfessionId == right.ProfessionId;
    }
}
