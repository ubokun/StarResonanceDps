// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed class PlayerStatusWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable, IRowReorderTarget
{
    private PlayerStatusWidgetSettingsConfig _settings;

    /// <summary>行の色。設定が変わったときだけ作る(毎回作ると、色が同じでも行の色の差し替えになる)。</summary>
    private IReadOnlyDictionary<int, Brush> _textBrushes;

    /// <summary>値の増減の色(テキストカラーの「数値の増加」「数値の減少」)。行の色と同じく設定が変わったときだけ作る。</summary>
    private Brush? _increaseBrush;
    private Brush? _decreaseBrush;

    private IReadOnlyList<int> _rowOrder;
    private PlayerRosterEntry? _player;

    /// <summary>行を掴んでいる間の、掴んだ並べ替えの単位と見た目だけの並び。掴んでいなければ null。</summary>
    private int? _reorderUnitAttrId;
    private List<int>? _previewRowOrder;

    private bool _isDisposed;

    /// <summary>
    /// 増減の色と三角を消す時刻に行を見直すタイマー。光っている行があるときだけ、一番早い時刻に1回だけ動く。
    /// </summary>
    private readonly DispatcherTimer _valueChangeTimer = new();

    public PlayerStatusWidgetViewModel(
        WidgetListItemViewModel statusWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
        : base(statusWidget, requestedCharacterId)
    {
        _settings = statusWidget.GetPlayerStatusSettingsSnapshot();
        _textBrushes = CreateTextBrushes(_settings);
        CreateValueChangeBrushes(_settings);
        _rowOrder = statusWidget.GetPlayerStatusRowOrderSnapshot();
        _valueChangeTimer.Tick += ValueChangeTimer_Tick;
        statusWidget.PlayerStatusSettingsChanged += Widget_PlayerStatusSettingsChanged;
        statusWidget.PlayerStatusRowOrderChanged += Widget_PlayerStatusRowOrderChanged;
        InitializePlayer(initialPlayer);
    }

    public WidgetListItemViewModel StatusWidget => PlayerWidget;

    /// <summary>自分だけを出す1枚の窓なので、タイトルはウィジェット名だけ。</summary>
    protected override bool ShowsPlayerIdentityInHeader => false;

    /// <summary>表示する行。行の部品は使い回し、更新では値だけを入れ替える(<see cref="SynchronizeRows"/>)。</summary>
    public ObservableCollection<PlayerStatusRowItem> PlayerStatus { get; } = [];

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        StatusWidget.PlayerStatusSettingsChanged -= Widget_PlayerStatusSettingsChanged;
        StatusWidget.PlayerStatusRowOrderChanged -= Widget_PlayerStatusRowOrderChanged;
        _valueChangeTimer.Stop();
        _valueChangeTimer.Tick -= ValueChangeTimer_Tick;
    }

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        _player = player;
        RefreshRows();
    }

    protected override WidgetDataStatus CreateDataStatus(GameReceptionSnapshot reception, PlayerRosterEntry? player)
        => WidgetDataStatusRules.ForPlayerStatus(reception);

    /// <summary>表示設定(保存かプレビュー)が変わった。色を作り直して行に当て直す。</summary>
    private void Widget_PlayerStatusSettingsChanged(object? sender, EventArgs e)
    {
        _settings = StatusWidget.GetPlayerStatusSettingsSnapshot();
        _textBrushes = CreateTextBrushes(_settings);
        CreateValueChangeBrushes(_settings);
        RefreshRows();
    }

    /// <summary>行の並び(保存か、設定の窓のプレビュー)が変わった。</summary>
    private void Widget_PlayerStatusRowOrderChanged(IReadOnlyList<int> rowOrder)
    {
        _rowOrder = rowOrder;
        RefreshRows();
    }

    /// <summary>
    /// 行を当て直す。行を掴んでいる間は止める(届いた値で行が動くと、掴んでいる行の位置が変わる)。
    /// 離したときに <see cref="EndRowReorder"/> が必ず当て直すので、止めた分はそこで反映される。
    /// </summary>
    private void RefreshRows()
    {
        if (_reorderUnitAttrId is not null)
        {
            return;
        }

        ApplyRows();
    }

    /// <summary>今の値で行を作り、位置ごとに当て直す。掴んでいる間は見た目だけの並びで並べる。</summary>
    private void ApplyRows()
    {
        var nowUtc = DateTime.UtcNow;
        IReadOnlyList<PlayerStatusRow> rows = _player is null
            ? []
            : PlayerStatusEntry.Create(
                _player,
                _settings.HideInactiveStatusEffects
                    ?? WidgetConfigDefaults.DefaultHideInactiveStatusEffects,
                _settings.ShowActualValues
                    ?? WidgetConfigDefaults.DefaultPlayerStatusShowActualValues,
                _settings.RowVisibility,
                _textBrushes,
                _previewRowOrder ?? _rowOrder,
                nowUtc,
                _increaseBrush,
                _decreaseBrush,
                GameReceptionStateStore.Current.HasSelfEnterScene);

        SynchronizeRows(rows);
        ScheduleValueChangeTimer(rows, nowUtc);
    }

    /// <summary>光っている行の、一番早く消える時刻にタイマーを合わせる。無ければ止める。</summary>
    private void ScheduleValueChangeTimer(IReadOnlyList<PlayerStatusRow> rows, DateTime nowUtc)
    {
        _valueChangeTimer.Stop();
        if (_isDisposed)
        {
            return;
        }

        DateTime? earliestEndUtc = null;
        foreach (var row in rows)
        {
            if (row.ValueChange != StatusValueChange.None
                && (earliestEndUtc is null || row.ValueChangeEndsAtUtc < earliestEndUtc))
            {
                earliestEndUtc = row.ValueChangeEndsAtUtc;
            }
        }

        if (earliestEndUtc is not { } endUtc)
        {
            return;
        }

        var interval = endUtc - nowUtc;
        _valueChangeTimer.Interval = interval > TimeSpan.Zero ? interval : TimeSpan.Zero;
        _valueChangeTimer.Start();
    }

    private void ValueChangeTimer_Tick(object? sender, EventArgs e)
    {
        _valueChangeTimer.Stop();
        RefreshRows();
    }

    /// <summary>
    /// 行を位置ごとに当て直す。行の部品は作り直さず値だけを入れ替え、増えた分だけ足し、減った分だけ末尾から外す
    /// (行が途中で出入りしても、後ろの行は値が1つずれて入るだけ)。
    /// </summary>
    private void SynchronizeRows(IReadOnlyList<PlayerStatusRow> rows)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (index < PlayerStatus.Count)
            {
                PlayerStatus[index].Apply(rows[index]);
                continue;
            }

            var item = new PlayerStatusRowItem();
            item.Apply(rows[index]);
            PlayerStatus.Add(item);
        }

        while (PlayerStatus.Count > rows.Count)
        {
            PlayerStatus.RemoveAt(PlayerStatus.Count - 1);
        }
    }

    /// <summary>行の並べ替えの単位(<see cref="PlayerStatusRowItem.OrderUnitAttrId"/>)。行は位置ごとに値を入れ替えるので、今その位置に出ている行の単位。</summary>
    public object? GetRowReorderKey(object item)
    {
        return item is PlayerStatusRowItem row ? row.OrderUnitAttrId : null;
    }

    /// <summary>
    /// 行を掴み始めた。離すまで、届いた値による行の当て直しを止める(値で行が動くと、掴んでいる行の位置が変わる)。
    /// </summary>
    public void BeginRowReorder(object key)
    {
        if (key is not int unitAttrId)
        {
            return;
        }

        _reorderUnitAttrId = unitAttrId;
        _previewRowOrder = [.. _rowOrder];
    }

    /// <summary>
    /// 掴んでいる間の見た目だけの並び。<b>隠れている行も含めた全体の並び</b>の中で、掴んだ単位を <paramref name="beforeKey"/> の単位の前
    /// (null なら末尾)へ動かし、行を位置ごとに当て直す(行の部品は作り直さない)。
    /// </summary>
    public void ShowRowReorderAt(object? beforeKey)
    {
        if (_reorderUnitAttrId is not { } unitAttrId || Equals(beforeKey, unitAttrId))
        {
            return;
        }

        var order = new List<int>(_rowOrder);
        if (!order.Remove(unitAttrId))
        {
            return;
        }

        var insertAt = beforeKey is int before ? order.IndexOf(before) : order.Count;
        if (insertAt < 0)
        {
            return;
        }

        order.Insert(insertAt, unitAttrId);
        if (_previewRowOrder is not null && order.SequenceEqual(_previewRowOrder))
        {
            return;
        }

        _previewRowOrder = order;
        ApplyRows();
    }

    /// <summary>掴んでいる間に、掴んだ単位を元の位置へ戻す(窓の外にいる間)。</summary>
    public void ShowRowReorderAtOriginalPosition()
    {
        if (_reorderUnitAttrId is null)
        {
            return;
        }

        _previewRowOrder = [.. _rowOrder];
        ApplyRows();
    }

    /// <summary>
    /// 掴み終わり。<paramref name="commit"/> なら見た目の並びを確定する: 設定の窓が開いていなければその場で保存し(ウィンドウの位置と同じ扱い)、
    /// 開いていれば保存前の並びとして設定の窓へ渡す。止めていた行の当て直しはここで1回だけ反映する。
    /// </summary>
    public void EndRowReorder(bool commit)
    {
        if (_reorderUnitAttrId is null)
        {
            return;
        }

        var previewRowOrder = _previewRowOrder;
        _reorderUnitAttrId = null;
        _previewRowOrder = null;

        if (commit && previewRowOrder is not null && !previewRowOrder.SequenceEqual(_rowOrder))
        {
            StatusWidget.ChangePlayerStatusRowOrderFromWidget(previewRowOrder);
        }

        ApplyRows();
    }

    /// <summary>設定の各行の選択色を、行の鍵ごとの刷にする。</summary>
    private static IReadOnlyDictionary<int, Brush> CreateTextBrushes(PlayerStatusWidgetSettingsConfig settings)
    {
        var brushes = new Dictionary<int, Brush>(PlayerStatusEntry.SettingRowAttrIds.Count);
        var palettes = settings.TextColorPalettes;
        var indexes = settings.TextColorIndexes;
        if (palettes is null)
        {
            return brushes;
        }

        foreach (var attrId in PlayerStatusEntry.SettingRowAttrIds)
        {
            if (TryCreatePaletteBrush(palettes, indexes, attrId.ToString(CultureInfo.InvariantCulture), out var brush))
            {
                brushes[attrId] = brush;
            }
        }

        return brushes;
    }

    /// <summary>設定の「数値の増加」「数値の減少」の選択色を刷にする。</summary>
    private void CreateValueChangeBrushes(PlayerStatusWidgetSettingsConfig settings)
    {
        _increaseBrush = null;
        _decreaseBrush = null;
        if (settings.TextColorPalettes is not { } palettes)
        {
            return;
        }

        if (TryCreatePaletteBrush(palettes, settings.TextColorIndexes, WidgetConfigDefaults.PlayerStatusValueIncreaseTextColorKey, out var increase))
        {
            _increaseBrush = increase;
        }

        if (TryCreatePaletteBrush(palettes, settings.TextColorIndexes, WidgetConfigDefaults.PlayerStatusValueDecreaseTextColorKey, out var decrease))
        {
            _decreaseBrush = decrease;
        }
    }

    /// <summary>テキストカラーの鍵の選択色を、凍らせた刷にする。色が無い・読めないなら偽。</summary>
    private static bool TryCreatePaletteBrush(
        IReadOnlyDictionary<string, List<string>> palettes,
        IReadOnlyDictionary<string, int>? indexes,
        string key,
        out Brush brush)
    {
        brush = null!;
        if (!palettes.TryGetValue(key, out var palette) || palette is not { Count: > 0 })
        {
            return false;
        }

        var index = indexes is not null && indexes.TryGetValue(key, out var saved)
            ? saved
            : WidgetConfigDefaults.DefaultPlayerStatusTextColorIndex;

        if (!ColorUtilities.TryParseHex(palette[Math.Clamp(index, 0, palette.Count - 1)], out var color))
        {
            return false;
        }

        var solid = new SolidColorBrush(color);
        solid.Freeze();
        brush = solid;
        return true;
    }
}
