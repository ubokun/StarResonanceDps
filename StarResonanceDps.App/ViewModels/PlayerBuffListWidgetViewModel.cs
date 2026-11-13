// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class PlayerBuffListWidgetViewModel : PlayerWidgetWindowViewModel, IBuffListWindowViewModel, IDisposable
{
    private readonly PlayerBuffListKind _kind;

    /// <summary>右クリックの「グループに追加」の行き先。</summary>
    private readonly IBuffDebuffGroupEntryAdder? _groupEntryAdder;

    /// <summary>右クリックの「バフ・デバフカードのショートカットに追加」の行き先。</summary>
    private readonly IBuffCardShortcutAdder? _shortcutAdder;

    /// <summary>行をクリックしたときにバフ・デバフカードを開く経路。</summary>
    private readonly Action<long, PlayerBuffListKind, string, int>? _openCard;

    private readonly ObservableCollection<PlayerBuffEntry> _entries = [];
    private readonly List<PlayerBuffEntry> _orderedEntries = [];
    private readonly DispatcherTimer _refreshTimer;
    private BuffListWidgetSettingsConfig _settings;

    /// <summary>最後に行を当てた相手。名簿の行が変わっても相手が同じなら、行は1秒ごとの更新に任せる。</summary>
    private long? _refreshedCharacterId;

    /// <summary>名簿の対象者の行。行の名前の対象者名とレベル(<c>{TargetName}</c> <c>{Level}</c>)に使う。</summary>
    private PlayerRosterEntry? _player;

    private bool _isDisposed;

    /// <summary>行のゲージの塗り。設定が変わったら作り直す。</summary>
    [ObservableProperty]
    private Brush _gaugeBrush = Brushes.Transparent;

    /// <summary>右クリックのメニューを開いた行。優先表示の項目の文言はこの行で決める。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PriorityMenuText))]
    private PlayerBuffEntry? _menuEntry;

    public PlayerBuffListWidgetViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer,
        PlayerBuffListKind kind,
        IBuffDebuffGroupEntryAdder? groupEntryAdder = null,
        IBuffCardShortcutAdder? shortcutAdder = null,
        Action<long, PlayerBuffListKind, string, int>? openCard = null)
        : base(playerWidget, requestedCharacterId)
    {
        _kind = kind;
        _groupEntryAdder = groupEntryAdder;
        _shortcutAdder = shortcutAdder;
        _openCard = openCard;
        _settings = playerWidget.GetBuffListSettingsSnapshot();
        GaugeBrush = BuffGaugeBrush.Create(_settings.GaugeColors);
        Entries = new ReadOnlyObservableCollection<PlayerBuffEntry>(_entries);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        playerWidget.BuffListSettingsChanged += Widget_BuffListSettingsChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        InitializePlayer(initialPlayer);
        Refresh();
        _refreshTimer.Start();
    }

    public ReadOnlyObservableCollection<PlayerBuffEntry> Entries { get; }

    public WidgetListItemViewModel Widget => PlayerWidget;

    public string CardMenuText => LocalizationManager.Instance.GetString(
        _kind == PlayerBuffListKind.Debuff
            ? "BuffList_Menu_DebuffCard"
            : "BuffList_Menu_BuffCard");

    public string PriorityMenuText => BuffDebuffCardContent.GetPriorityMenuText(
        _kind == PlayerBuffListKind.Debuff,
        MenuEntry is { } entry && PlayerWidget.IsBuffListPriorityBuff(entry.BaseId));

    public void RefreshMenuText()
    {
        OnPropertyChanged(nameof(PriorityMenuText));
    }

    public string HideMenuText => LocalizationManager.Instance.GetString(
        _kind == PlayerBuffListKind.Debuff
            ? "BuffList_Menu_HideDebuff"
            : "BuffList_Menu_HideBuff");

    public string AddToGroupMenuText => LocalizationManager.Instance.GetString("BuffList_Menu_AddToGroup");

    /// <summary>自分が対象の一覧だけ。料理・薬剤はまとまりの行として足す(<see cref="BuffEntryKey.ForBuffDebuffGroup"/>)。</summary>
    public bool CanAddToGroup(PlayerBuffEntry? entry)
    {
        return entry is not null
            && IsSelfTarget
            && _groupEntryAdder is not null
            && _groupEntryAdder.CanAdd(BuffEntryKey.ForBuffDebuffGroup(entry.BaseId));
    }

    public string AddToShortcutsMenuText => LocalizationManager.Instance.GetString("BuffList_Menu_AddToBuffCardShortcuts");

    /// <summary>自分が対象の一覧だけ(ショートカットの相手はいつも自分)。料理・薬剤はまとまりの行として足す(グループと同じ)。</summary>
    public bool CanAddToShortcuts(PlayerBuffEntry? entry)
    {
        return entry is not null
            && IsSelfTarget
            && _shortcutAdder is not null
            && _shortcutAdder.CanAddShortcut(BuffEntryKey.ForBuffDebuffGroup(entry.BaseId));
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
        PlayerWidget.BuffListSettingsChanged -= Widget_BuffListSettingsChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    /// <summary>
    /// 表示設定(保存かプレビュー)が変わった。色を作り直し、行も作り直す
    /// (優先表示と非表示のバフを効かせる。ゲージの長さもそこで当て直る)。
    /// </summary>
    private void Widget_BuffListSettingsChanged(object? sender, EventArgs e)
    {
        _settings = PlayerWidget.GetBuffListSettingsSnapshot();
        GaugeBrush = BuffGaugeBrush.Create(_settings.GaugeColors);
        Refresh();
    }

    /// <summary>
    /// 名簿の行が変わった。相手が替わったときだけ行を当て直す。同じ相手なら、タイトルは基底が組み直しているので何もしない
    /// (バフは名簿の行から作らないので、名簿の変化のたびに当て直す意味が無い)。
    /// </summary>
    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        // 名前とレベルは控えるだけ。行の名前は1秒ごとの更新で組み直る。
        _player = player;

        if (SelectedCharacterId == _refreshedCharacterId)
        {
            return;
        }

        Refresh();
    }

    [RelayCommand]
    private void OpenCard(PlayerBuffEntry? entry)
    {
        if (entry is null || SelectedCharacterId is not { } characterId)
        {
            return;
        }

        _openCard?.Invoke(characterId, _kind, entry.CardKey, entry.BaseId);
    }

    [RelayCommand]
    private void TogglePriority(PlayerBuffEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        PlayerWidget.SetBuffListPriorityBuffEnabledFromWidget(
            entry.BaseId,
            !PlayerWidget.IsBuffListPriorityBuff(entry.BaseId));
    }

    [RelayCommand]
    private void HideBuff(PlayerBuffEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        PlayerWidget.HideBuffInList(entry.BaseId);
    }

    [RelayCommand]
    private void AddToGroup(PlayerBuffEntry? entry)
    {
        if (!CanAddToGroup(entry))
        {
            return;
        }

        _groupEntryAdder!.Add(BuffEntryKey.ForBuffDebuffGroup(entry!.BaseId));
    }

    [RelayCommand]
    private void AddToShortcuts(PlayerBuffEntry? entry)
    {
        if (!CanAddToShortcuts(entry))
        {
            return;
        }

        _shortcutAdder!.AddShortcut(BuffEntryKey.ForBuffDebuffGroup(entry!.BaseId));
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
        // 出現時の一覧の受信は名簿の行を変えないことがあるので、読み直すたびに見る。
        RefreshDataStatus();
    }

    protected override WidgetDataStatus CreateDataStatus(GameReceptionSnapshot reception, PlayerRosterEntry? player)
        => WidgetDataStatusRules.ForPlayerBuffs(
            reception,
            IsSelfTarget,
            player,
            SelectedCharacterId is { } characterId && MeterSnapshotProvider.HasReceivedPlayerAppearBuffList(characterId));

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CardMenuText));
        OnPropertyChanged(nameof(PriorityMenuText));
        OnPropertyChanged(nameof(HideMenuText));
        OnPropertyChanged(nameof(AddToGroupMenuText));
        OnPropertyChanged(nameof(AddToShortcutsMenuText));
        Refresh();
    }

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        _refreshedCharacterId = SelectedCharacterId;
        if (SelectedCharacterId is not { } characterId)
        {
            SynchronizeEntries(Array.Empty<PlayerBuffSnapshot>(), string.Empty, 0);
            return;
        }

        var playerIdentity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        if (playerIdentity is not null)
        {
            SetHeaderText(playerIdentity.Name, playerIdentity.UserId, playerIdentity.IsNpc, playerIdentity.ProfessionId);
        }

        // 対象者名はバフ・デバフカードと同じ規則(名前がまだ無ければ UID、伏せる設定なら伏せ字、NPC なら職業名)。
        var targetName = PlayerInfoFormatFormatter.GetDisplayName(
            _player?.Name ?? playerIdentity?.Name,
            playerIdentity?.UserId ?? characterId,
            _player?.IsSelf ?? (AppState.PlayerUID != 0 && characterId == AppState.PlayerUID),
            _player?.IsNpc ?? playerIdentity?.IsNpc ?? false,
            _player?.ProfessionId ?? playerIdentity?.ProfessionId ?? 0,
            PlayerRosterPresentationStore.Instance.NameDisplayMode);

        SynchronizeEntries(BuffListSnapshotFilter.GetPlayerRows(_settings, characterId, _kind), targetName, _player?.Level ?? 0);
    }

    private void SynchronizeEntries(IReadOnlyList<PlayerBuffSnapshot> snapshots, string targetName, int level)
    {
        var snapshotKeys = snapshots
            .Select(snapshot => snapshot.Key)
            .ToHashSet(StringComparer.Ordinal);

        for (var index = _entries.Count - 1; index >= 0; index--)
        {
            if (snapshotKeys.Contains(_entries[index].Key))
            {
                continue;
            }

            // 行が消えるならメニューも閉じる(消えた行に開いたまま残らないように)。
            _entries[index].IsMenuOpen = false;
            _entries.RemoveAt(index);
        }

        for (var index = 0; index < snapshots.Count; index++)
        {
            var snapshot = snapshots[index];
            var name = BuffListRowName.Format(_settings, snapshot, targetName, level);
            var existingIndex = FindEntryIndex(snapshot.Key);
            if (existingIndex < 0)
            {
                _orderedEntries.Add(new PlayerBuffEntry(snapshot, name));
                continue;
            }

            var entry = _entries[existingIndex];
            entry.Update(snapshot, name);
            _orderedEntries.Add(entry);
        }

        CollectionOrder.Apply(_entries, _orderedEntries);
        _orderedEntries.Clear();

        // 行を入れ替えた後にまとめて計算する。作った直後の行にも同じ長さが当たる。
        var gaugeLengthSeconds = BuffGaugeBrush.GetLengthSeconds(_settings.GaugeLengthIndex);
        foreach (var entry in _entries)
        {
            entry.UpdateBar(gaugeLengthSeconds);
        }
    }

    private int FindEntryIndex(string key)
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            if (string.Equals(_entries[index].Key, key, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
