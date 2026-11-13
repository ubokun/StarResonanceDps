// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// バフの番号か料理・薬剤のまとまり(<see cref="BuffEntryKey"/>)の行を、スイッチと掴んでの並べ替えで出す一覧(設定の窓の節)。
/// バフ・デバフグループの「バフ・デバフ」とバフ・デバフカードの「ショートカット」、両方の「通知対象」、
/// バフ・デバフ一覧とエンティティ情報の「優先表示リスト」が使う。
///
/// <para>
/// 行はスイッチでオン/オフし、掴んで並べ替える(<see cref="IRowReorderTarget"/>。並べ替えの注意書きの鍵が無い一覧は並べ替えない)。
/// どちらも保存前のプレビューで、確定は持ち主の設定の「保存」。
/// 右クリックで足した行はオフのまま保存すると消え、既定の行(持ち主が渡す既定の一覧)は消えない。
/// 変わったことは、持ち主が渡す <c>onChanged</c> で知らせる(読み込みでは呼ばない)。
/// </para>
/// </summary>
public sealed partial class BuffEntryListSettingsViewModel : ObservableObject, IRowReorderTarget
{
    private readonly ObservableCollection<BuffSettingRowViewModel> _entries = [];
    private readonly IReadOnlyList<(BuffEntryKey Key, bool IsEnabled)> _defaults;
    private readonly string _titleResourceKey;
    private readonly string _descriptionResourceKey;
    private readonly string? _reorderNoteResourceKey;
    private readonly Action _onChanged;

    /// <summary>掴んでいる行の鍵と、掴んだときの並び。掴んでいなければ null。</summary>
    private BuffEntryKey? _draggingKey;
    private List<BuffEntryKey>? _orderBeforeDrag;

    /// <param name="defaults">既定の行(オフにしても消えない行)。</param>
    /// <param name="titleResourceKey">節の名前の文言の鍵。</param>
    /// <param name="descriptionResourceKey">見出しの下の1つ目の注意書き(行の足し方)の文言の鍵。</param>
    /// <param name="reorderNoteResourceKey">
    /// 並べ替えの注意書きの文言の鍵(ウィジェットでも並べ替えられるかで違う)。null なら並べ替えない一覧で、注意書きも出さない(並びに意味の無い「通知対象」)。
    /// </param>
    /// <param name="onChanged">スイッチを触ったときと、掴んで並びを変えて離したときに呼ぶ。</param>
    public BuffEntryListSettingsViewModel(
        IReadOnlyList<(BuffEntryKey Key, bool IsEnabled)> defaults,
        string titleResourceKey,
        string descriptionResourceKey,
        string? reorderNoteResourceKey,
        Action onChanged)
    {
        _defaults = defaults;
        _titleResourceKey = titleResourceKey;
        _descriptionResourceKey = descriptionResourceKey;
        _reorderNoteResourceKey = reorderNoteResourceKey;
        _onChanged = onChanged;
        Entries = new ReadOnlyObservableCollection<BuffSettingRowViewModel>(_entries);
    }

    /// <summary>一覧の行。並びはウィジェット(選択リスト)の並び。</summary>
    public ReadOnlyObservableCollection<BuffSettingRowViewModel> Entries { get; }

    public string SectionTitle => LocalizationManager.Instance.GetString(_titleResourceKey);

    public string Description => LocalizationManager.Instance.GetString(_descriptionResourceKey);

    /// <summary>行を掴んで並べ替えられる一覧か。偽なら並べ替えの注意書きを出さない。</summary>
    public bool AllowsReorder => _reorderNoteResourceKey is not null;

    public string ReorderNote => _reorderNoteResourceKey is { } key
        ? LocalizationManager.Instance.GetString(key)
        : string.Empty;

    /// <summary>保存する行。右クリックで足した行は、スイッチがオフなら落とす(保存すると行が消える)。既定の行はオフでも残す。</summary>
    public List<BuffDebuffGroupEntryConfig> CreateEntries()
    {
        return
        [
            .. _entries
                .Where(row => row.IsEnabled || row.IsDefault)
                .Select(row => new BuffDebuffGroupEntryConfig
                {
                    BuffId = row.Key.BuffId,
                    Group = row.Key.Group,
                    IsEnabled = row.IsEnabled
                })
        ];
    }

    /// <summary>行を作り直す(読み込み・初期化・保存の後)。変わったことは知らせない。</summary>
    public void Load(IReadOnlyList<BuffDebuffGroupEntryConfig>? entries)
    {
        _entries.Clear();
        foreach (var entry in WidgetConfigDefaults.NormalizeBuffEntries(entries, _defaults))
        {
            _entries.Add(CreateRow(entry.GetKey(), entry.IsEnabled));
        }

        UpdateIsLast();
    }

    /// <summary>
    /// 右クリックで行がオン/オフになった分を行に入れる(<see cref="WidgetConfigDefaults.SetBuffEntryEnabled"/> と同じ決まり)。
    /// 既に保存されている分なので、変わったことは知らせない。
    /// </summary>
    public void SetEntryEnabled(BuffEntryKey key, bool isEnabled)
    {
        var row = _entries.FirstOrDefault(candidate => candidate.Key == key);
        if (isEnabled)
        {
            if (row is not null)
            {
                row.LoadEnabled(true);
            }
            else
            {
                _entries.Add(CreateRow(key, isEnabled: true));
            }
        }
        else if (row is not null)
        {
            if (row.IsDefault)
            {
                row.LoadEnabled(false);
            }
            else
            {
                _entries.Remove(row);
            }
        }

        UpdateIsLast();
    }

    /// <summary>行を鍵の並びのとおりに並べ直す(作り直さない)。ウィジェットで並べ替えた分を入れるのに使う。変わったことは知らせない。</summary>
    public void ApplyOrder(IReadOnlyList<BuffEntryKey> order)
    {
        MoveToOrder(order);
    }

    public void RefreshLocalizedText()
    {
        foreach (var row in _entries)
        {
            row.RefreshMetadata();
        }

        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(ReorderNote));
    }

    /// <summary>2つの保存する行が、並び・鍵・オン/オフまで同じか。</summary>
    public static bool EntriesEqual(
        IReadOnlyList<BuffDebuffGroupEntryConfig>? left,
        IReadOnlyList<BuffDebuffGroupEntryConfig>? right)
    {
        var leftEntries = left ?? [];
        var rightEntries = right ?? [];
        return leftEntries.Count == rightEntries.Count
            && leftEntries
                .Zip(rightEntries)
                .All(pair => pair.First.GetKey() == pair.Second.GetKey() && pair.First.IsEnabled == pair.Second.IsEnabled);
    }

    /// <summary>行の並べ替えの単位(行1つずつ)。並べ替えない一覧は null を返して掴ませない。</summary>
    public object? GetRowReorderKey(object item)
    {
        return AllowsReorder && item is BuffSettingRowViewModel row ? row.Key : null;
    }

    public void BeginRowReorder(object key)
    {
        if (key is BuffEntryKey entryKey)
        {
            _draggingKey = entryKey;
            _orderBeforeDrag = [.. _entries.Select(row => row.Key)];
        }
    }

    /// <summary>掴んでいる間の見た目の並び。掴んだ行を <paramref name="beforeKey"/> の行の前(null なら末尾)へ動かす。プレビューは離したときに出す。</summary>
    public void ShowRowReorderAt(object? beforeKey)
    {
        if (_draggingKey is not { } draggingKey || Equals(beforeKey, draggingKey))
        {
            return;
        }

        var currentIndex = IndexOf(draggingKey);
        if (currentIndex < 0)
        {
            return;
        }

        var row = _entries[currentIndex];
        var order = _entries.Where(candidate => candidate.Key != draggingKey).ToList();
        var insertAt = beforeKey is BuffEntryKey before
            ? order.FindIndex(candidate => candidate.Key == before)
            : order.Count;
        if (insertAt < 0)
        {
            return;
        }

        order.Insert(insertAt, row);
        MoveToOrder(order.Select(candidate => candidate.Key).ToList());
    }

    /// <summary>掴んでいる間に、掴んだ行を元の位置(掴んだときの並び)へ戻す。設定の窓の外に出たときに使う。</summary>
    public void ShowRowReorderAtOriginalPosition()
    {
        if (_draggingKey is null || _orderBeforeDrag is null)
        {
            return;
        }

        MoveToOrder(_orderBeforeDrag);
    }

    /// <summary>掴み終わり。<paramref name="commit"/> で並びが変わっていれば知らせ(保存は「保存」)、取り消しなら元の並びへ戻す。</summary>
    public void EndRowReorder(bool commit)
    {
        if (_draggingKey is null || _orderBeforeDrag is null)
        {
            return;
        }

        var orderBeforeDrag = _orderBeforeDrag;
        _draggingKey = null;
        _orderBeforeDrag = null;

        if (!commit)
        {
            MoveToOrder(orderBeforeDrag);
            return;
        }

        if (!_entries.Select(row => row.Key).SequenceEqual(orderBeforeDrag))
        {
            _onChanged();
        }
    }

    private BuffSettingRowViewModel CreateRow(BuffEntryKey key, bool isEnabled)
    {
        return new BuffSettingRowViewModel(
            key,
            WidgetConfigDefaults.IsDefaultBuffEntry(_defaults, key),
            isEnabled,
            _onChanged);
    }

    private int IndexOf(BuffEntryKey key)
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            if (_entries[index].Key == key)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>行を鍵の並びのとおりの位置へ動かす(作り直さない)。最後の行の印も付け直す。</summary>
    private void MoveToOrder(IReadOnlyList<BuffEntryKey> orderedKeys)
    {
        for (var target = 0; target < orderedKeys.Count && target < _entries.Count; target++)
        {
            var current = IndexOf(orderedKeys[target]);
            if (current >= 0 && current != target)
            {
                _entries.Move(current, target);
            }
        }

        UpdateIsLast();
    }

    private void UpdateIsLast()
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            _entries[index].IsLast = index == _entries.Count - 1;
        }
    }
}
