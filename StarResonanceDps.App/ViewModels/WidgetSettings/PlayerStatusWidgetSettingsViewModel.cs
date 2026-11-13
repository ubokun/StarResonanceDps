// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// ステータス詳細の表示設定、行ごとのオン/オフ、行ごとのテキストカラー。
/// 行の一覧の作りは「他人のロールスキル」(<see cref="MeterWidgetSettingsViewModel"/>)、
/// テキストカラーはタイムライン(<see cref="TimelineWidgetSettingsViewModel"/>)と同じ。
/// </summary>
public sealed partial class PlayerStatusWidgetSettingsViewModel : ObservableObject, IDisposable, IRowReorderTarget
{
    private readonly ObservableCollection<PlayerStatusRowItemViewModel> _rows = [];
    private readonly ObservableCollection<PlayerStatusTextColorItemViewModel> _textColors = [];
    private readonly Dictionary<string, PlayerStatusTextColorItemViewModel> _textColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private PlayerStatusWidgetSettingsConfig _lastSaved;

    /// <summary>行の並び(<c>WidgetConfig.PlayerStatusRowOrder</c>、並べ替えの単位の番号)。保存前の今の並びと、保存された並び。</summary>
    private List<int> _rowOrder;
    private List<int> _lastSavedRowOrder;

    /// <summary>「ステータス」の一覧で掴んでいる並べ替えの単位。掴んでいなければ null。</summary>
    private int? _draggingUnitAttrId;
    private bool _isLoading;

    [ObservableProperty]
    private bool _hideInactiveStatusEffects = WidgetConfigDefaults.DefaultHideInactiveStatusEffects;

    [ObservableProperty]
    private bool _showActualValues = WidgetConfigDefaults.DefaultPlayerStatusShowActualValues;

    /// <param name="rowOrder">保存された行の並び(<c>WidgetConfig.PlayerStatusRowOrder</c>)。2つの一覧をこの順で並べる。</param>
    public PlayerStatusWidgetSettingsViewModel(PlayerStatusWidgetSettingsConfig? config, IReadOnlyList<int>? rowOrder)
    {
        _rowOrder = WidgetConfigDefaults.NormalizePlayerStatusRowOrder(rowOrder);
        _lastSavedRowOrder = [.. _rowOrder];

        var attrIds = PlayerStatusEntry.OrderSettingRowAttrIds(_rowOrder);
        for (var index = 0; index < attrIds.Count; index++)
        {
            _rows.Add(new PlayerStatusRowItemViewModel(
                attrIds[index],
                index == attrIds.Count - 1,
                OnRowVisibilityChanged));
        }

        Rows = new ReadOnlyObservableCollection<PlayerStatusRowItemViewModel>(_rows);

        var textColorKeys = OrderTextColorKeys(attrIds);
        for (var index = 0; index < textColorKeys.Count; index++)
        {
            var key = textColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultPlayerStatusTextColors(key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += TextColors_PaletteChanged;

            var item = new PlayerStatusTextColorItemViewModel(key, colors, index == textColorKeys.Count - 1);
            _textColorItemsByKey.Add(item.Key, item);
            _textColors.Add(item);
        }

        TextColors = new ReadOnlyObservableCollection<PlayerStatusTextColorItemViewModel>(_textColors);

        Load(WidgetConfigDefaults.CloneNormalizedPlayerStatus(config));
        _lastSaved = CreateConfig();

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    public event Action<PlayerStatusWidgetSettingsConfig>? PreviewChanged;

    /// <summary>「ステータス」の一覧で行を並べ替えた(または破棄で保存された並びへ戻す)。引数は保存前の並び。</summary>
    public event Action<IReadOnlyList<int>>? RowOrderPreviewChanged;

    public ReadOnlyObservableCollection<PlayerStatusRowItemViewModel> Rows { get; }

    public ReadOnlyObservableCollection<PlayerStatusTextColorItemViewModel> TextColors { get; }

    public string SectionTitle => LocalizationManager.Instance.GetString("Settings_Section_Display_Title");

    public string RowSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_PlayerStatusRows_Title");

    public string TextColorSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_PlayerStatusTextColors_Title");

    /// <summary>スイッチの右に出す状態の文言。作りはウィンドウの表示設定と同じ。</summary>
    public string HideInactiveStatusEffectsStateText => LocalizationManager.Instance.GetString(
        HideInactiveStatusEffects ? "Settings_Switch_On" : "Settings_Switch_Off");

    public string ShowActualValuesStateText => LocalizationManager.Instance.GetString(
        ShowActualValues ? "Settings_Switch_On" : "Settings_Switch_Off");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved)
        || !_rowOrder.SequenceEqual(_lastSavedRowOrder);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in _textColors)
        {
            item.Colors.PaletteChanged -= TextColors_PaletteChanged;
        }
    }

    public PlayerStatusWidgetSettingsConfig CreateConfig()
    {
        var config = new PlayerStatusWidgetSettingsConfig
        {
            HideInactiveStatusEffects = HideInactiveStatusEffects,
            ShowActualValues = ShowActualValues,
            RowVisibility = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
            TextColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            TextColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var row in _rows)
        {
            config.RowVisibility[row.Key] = row.IsVisible;
        }

        foreach (var item in _textColors)
        {
            config.TextColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.TextColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        return config;
    }

    /// <summary>既定に戻す。行の並びも既定の並びに戻す(ほかの項目と同じく保存前の値。確定は「保存」)。</summary>
    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreatePlayerStatusSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();

        var defaultRowOrder = WidgetConfigDefaults.NormalizePlayerStatusRowOrder(null);
        if (!defaultRowOrder.SequenceEqual(_rowOrder))
        {
            ApplyRowOrder(defaultRowOrder);
            RowOrderPreviewChanged?.Invoke([.. _rowOrder]);
        }
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
        RowOrderPreviewChanged?.Invoke([.. _lastSavedRowOrder]);
    }

    /// <param name="rowOrder">一緒に保存した行の並び。</param>
    public void MarkSaved(PlayerStatusWidgetSettingsConfig? config, IReadOnlyList<int>? rowOrder)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedPlayerStatus(config);
        _lastSavedRowOrder = WidgetConfigDefaults.NormalizePlayerStatusRowOrder(rowOrder);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>保存前の今の行の並び。「保存」でこの並びを保存する。</summary>
    public List<int> GetRowOrderSnapshot()
    {
        return [.. _rowOrder];
    }

    /// <summary>色選択ウィンドウの初期値。宛先はテキストカラーの鍵(<see cref="WidgetConfigDefaults.PlayerStatusTextColorKeys"/>)。</summary>
    public Color GetSelectedTextColor(string key)
    {
        return _textColorItemsByKey.TryGetValue(key, out var item)
            ? item.Colors.SelectedColor
            : Colors.White;
    }

    public void ApplyTextColor(string key, Color color)
    {
        if (_textColorItemsByKey.TryGetValue(key, out var item))
        {
            item.Colors.AddOrSelect(color);
        }
    }

    /// <summary>「ステータス」の一覧の行の並べ替えの単位(<see cref="PlayerStatusRowItemViewModel.OrderUnitAttrId"/>)。</summary>
    public object? GetRowReorderKey(object item)
    {
        return item is PlayerStatusRowItemViewModel row ? row.OrderUnitAttrId : null;
    }

    /// <summary>「ステータス」の一覧で行を掴み始めた。並びはまだ変えない。</summary>
    public void BeginRowReorder(object key)
    {
        if (key is int unitAttrId)
        {
            _draggingUnitAttrId = unitAttrId;
        }
    }

    /// <summary>
    /// 掴んでいる間の見た目の並び。掴んだ単位の行を <paramref name="beforeKey"/> の単位の前(null なら末尾)へ動かす。
    /// 保存前の並び・テキストカラーの一覧・ウィジェットへのプレビューは変えない(離したときの <see cref="EndRowReorder"/> で確定)。
    /// </summary>
    public void ShowRowReorderAt(object? beforeKey)
    {
        if (_draggingUnitAttrId is not { } unitAttrId || Equals(beforeKey, unitAttrId))
        {
            return;
        }

        var dragged = _rows.Where(row => row.OrderUnitAttrId == unitAttrId).ToList();
        var others = _rows.Where(row => row.OrderUnitAttrId != unitAttrId).ToList();
        var insertAt = beforeKey is int before
            ? others.FindIndex(row => row.OrderUnitAttrId == before)
            : others.Count;
        if (dragged.Count == 0 || insertAt < 0)
        {
            return;
        }

        others.InsertRange(insertAt, dragged);
        MoveToOrder(_rows, others.Select(row => row.Key).ToList(), row => row.Key);
        UpdateRowIsLast();
    }

    /// <summary>掴んでいる間に、掴んだ単位を元の位置(保存前の今の並びの位置)へ戻す。設定の窓の外に出たときに使う。</summary>
    public void ShowRowReorderAtOriginalPosition()
    {
        if (_draggingUnitAttrId is null)
        {
            return;
        }

        MoveToOrder(_rows, OrderedRowKeys(_rowOrder), row => row.Key);
        UpdateRowIsLast();
    }

    /// <summary>
    /// 掴み終わり。<paramref name="commit"/> なら今の見た目の並びを保存前の並びとして確定し、ウィジェットへプレビューを渡す(保存は「保存」)。
    /// 取り消しなら元の並びへ戻す。
    /// </summary>
    public void EndRowReorder(bool commit)
    {
        if (_draggingUnitAttrId is null)
        {
            return;
        }

        _draggingUnitAttrId = null;
        if (commit)
        {
            var order = WidgetConfigDefaults.NormalizePlayerStatusRowOrder(
                _rows.Select(row => row.OrderUnitAttrId).Distinct().ToList());
            if (!order.SequenceEqual(_rowOrder))
            {
                ApplyRowOrder(order);
                RowOrderPreviewChanged?.Invoke([.. _rowOrder]);
                return;
            }
        }

        ApplyRowOrder(_rowOrder);
    }

    /// <summary>
    /// 行の並びを当てる(ウィジェットで並べ替えたときと、<see cref="EndRowReorder"/>)。「ステータス」と「テキストカラー」の一覧を同じ順に並べ直す。
    /// 行の部品は作り直さず位置だけ動かし、最後の行の印を付け直す。保存前の並びとして持ち、保存は「保存」で行う。
    /// </summary>
    public void ApplyRowOrder(IReadOnlyList<int> rowOrder)
    {
        _rowOrder = WidgetConfigDefaults.NormalizePlayerStatusRowOrder(rowOrder);
        var attrIds = PlayerStatusEntry.OrderSettingRowAttrIds(_rowOrder);
        MoveToOrder(_rows, OrderedRowKeys(_rowOrder), row => row.Key);
        MoveToOrder(_textColors, OrderTextColorKeys(attrIds), item => item.Key);
        UpdateRowIsLast();

        for (var index = 0; index < _textColors.Count; index++)
        {
            _textColors[index].IsLast = index == _textColors.Count - 1;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>「ステータス」の一覧の行の鍵を、行の並びのとおりに並べる。</summary>
    private static IReadOnlyList<string> OrderedRowKeys(IReadOnlyList<int> rowOrder)
    {
        return PlayerStatusEntry.OrderSettingRowAttrIds(rowOrder)
            .Select(attrId => attrId.ToString(CultureInfo.InvariantCulture))
            .ToList();
    }

    /// <summary>「ステータス」の一覧の最後の行の印を、今の並びで付け直す。</summary>
    private void UpdateRowIsLast()
    {
        for (var index = 0; index < _rows.Count; index++)
        {
            _rows[index].IsLast = index == _rows.Count - 1;
        }
    }

    /// <summary>テキストカラーの鍵の並び。先頭は値の増減の2つのまま、続く行は「ステータス」の一覧と同じ順。</summary>
    private static IReadOnlyList<string> OrderTextColorKeys(IReadOnlyList<int> orderedAttrIds)
    {
        return
        [
            WidgetConfigDefaults.PlayerStatusValueIncreaseTextColorKey,
            WidgetConfigDefaults.PlayerStatusValueDecreaseTextColorKey,
            .. orderedAttrIds.Select(attrId => attrId.ToString(CultureInfo.InvariantCulture))
        ];
    }

    /// <summary>一覧の項目を、鍵の並びのとおりの位置へ動かす(作り直さない)。</summary>
    private static void MoveToOrder<T>(ObservableCollection<T> items, IReadOnlyList<string> orderedKeys, Func<T, string> keyOf)
    {
        for (var target = 0; target < orderedKeys.Count && target < items.Count; target++)
        {
            for (var current = target; current < items.Count; current++)
            {
                if (!string.Equals(keyOf(items[current]), orderedKeys[target], StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (current != target)
                {
                    items.Move(current, target);
                }

                break;
            }
        }
    }

    partial void OnHideInactiveStatusEffectsChanged(bool value)
    {
        OnPropertyChanged(nameof(HideInactiveStatusEffectsStateText));
        NotifyChanged();
    }

    partial void OnShowActualValuesChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowActualValuesStateText));
        NotifyChanged();
    }

    private void Load(PlayerStatusWidgetSettingsConfig config)
    {
        _isLoading = true;
        try
        {
            HideInactiveStatusEffects = config.HideInactiveStatusEffects
                ?? WidgetConfigDefaults.DefaultHideInactiveStatusEffects;
            ShowActualValues = config.ShowActualValues
                ?? WidgetConfigDefaults.DefaultPlayerStatusShowActualValues;

            foreach (var row in _rows)
            {
                row.LoadVisibility(
                    config.RowVisibility is not null
                    && config.RowVisibility.TryGetValue(row.Key, out var visible)
                        ? visible
                        : PlayerStatusEntry.IsRowVisibleByDefault(row.AttrId));
            }

            foreach (var item in _textColors)
            {
                var palette = config.TextColorPalettes is not null
                    && config.TextColorPalettes.TryGetValue(item.Key, out var saved)
                    && saved is { Count: > 0 }
                        ? saved
                        : item.CreateDefaultColors();
                var index = config.TextColorIndexes is not null
                    && config.TextColorIndexes.TryGetValue(item.Key, out var savedIndex)
                        ? savedIndex
                        : WidgetConfigDefaults.DefaultPlayerStatusTextColorIndex;

                item.Colors.Load(palette, index);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }




    private void TextColors_PaletteChanged(object? sender, EventArgs e)
    {
        NotifyChanged();
    }

    /// <summary>スイッチを触った瞬間に呼ぶ。プレビュー反映と「未保存あり」の判定を更新する。</summary>
    private void OnRowVisibilityChanged()
    {
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (_isLoading)
        {
            return;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    private void RaisePreviewChanged()
    {
        PreviewChanged?.Invoke(CreateConfig());
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var row in _rows)
        {
            row.RefreshMetadata();
        }

        foreach (var item in _textColors)
        {
            item.RefreshDisplayName();
        }

        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(RowSectionTitle));
        OnPropertyChanged(nameof(TextColorSectionTitle));
        OnPropertyChanged(nameof(HideInactiveStatusEffectsStateText));
        OnPropertyChanged(nameof(ShowActualValuesStateText));
    }

    private static bool SettingsEqual(
        PlayerStatusWidgetSettingsConfig left,
        PlayerStatusWidgetSettingsConfig right)
    {
        if (left.HideInactiveStatusEffects != right.HideInactiveStatusEffects
            || left.ShowActualValues != right.ShowActualValues)
        {
            return false;
        }

        foreach (var attrId in PlayerStatusEntry.SettingRowAttrIds)
        {
            var key = attrId.ToString(CultureInfo.InvariantCulture);
            if (left.RowVisibility is null
                || right.RowVisibility is null
                || !left.RowVisibility.TryGetValue(key, out var leftVisible)
                || !right.RowVisibility.TryGetValue(key, out var rightVisible)
                || leftVisible != rightVisible)
            {
                return false;
            }
        }

        foreach (var key in WidgetConfigDefaults.PlayerStatusTextColorKeys)
        {
            if (left.TextColorIndexes is null
                || right.TextColorIndexes is null
                || left.TextColorPalettes is null
                || right.TextColorPalettes is null
                || !left.TextColorIndexes.TryGetValue(key, out var leftIndex)
                || !right.TextColorIndexes.TryGetValue(key, out var rightIndex)
                || leftIndex != rightIndex
                || !left.TextColorPalettes.TryGetValue(key, out var leftPalette)
                || !right.TextColorPalettes.TryGetValue(key, out var rightPalette)
                || !leftPalette.SequenceEqual(rightPalette, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
