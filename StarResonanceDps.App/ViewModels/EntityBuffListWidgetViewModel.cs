// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class EntityBuffListWidgetViewModel
    : ViewModelBase, IEntityWidgetWindowViewModel, IBuffListWindowViewModel, IDisposable
{
    private readonly WidgetListItemViewModel _widget;
    private readonly EntityWindowTarget _target;
    private readonly PlayerBuffListKind _kind;

    /// <summary>行をクリックしたときにバフ・デバフカードを開く経路。</summary>
    private readonly Action<EntityWindowTarget, PlayerBuffListKind, string, int>? _openCard;

    private readonly ObservableCollection<PlayerBuffEntry> _entries = [];
    private readonly List<PlayerBuffEntry> _orderedEntries = [];
    private readonly DispatcherTimer _refreshTimer;
    private BuffListWidgetSettingsConfig _settings;
    private bool _isDisposed;

    /// <summary>行のゲージの塗り。設定はプレイヤーから開いた一覧と同じウィジェットのものを使う。</summary>
    [ObservableProperty]
    private Brush _gaugeBrush = Brushes.Transparent;

    [ObservableProperty]
    private string _headerText = string.Empty;

    /// <summary>右クリックのメニューを開いた行。優先表示の項目の文言はこの行で決める。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PriorityMenuText))]
    private PlayerBuffEntry? _menuEntry;

    public EntityBuffListWidgetViewModel(
        WidgetListItemViewModel widget,
        EntityWindowTarget target,
        PlayerBuffListKind kind,
        Action<EntityWindowTarget, PlayerBuffListKind, string, int>? openCard = null)
    {
        _widget = widget;
        _target = target;
        _kind = kind;
        _openCard = openCard;
        _settings = widget.GetBuffListSettingsSnapshot();
        GaugeBrush = BuffGaugeBrush.Create(_settings.GaugeColors);
        Entries = new ReadOnlyObservableCollection<PlayerBuffEntry>(_entries);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _target.Changed += Target_Changed;
        _widget.BuffListSettingsChanged += Widget_BuffListSettingsChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        RefreshPresentation();
        Refresh();
        RefreshDataStatus();
        _refreshTimer.Start();
    }

    public WidgetDataStatus DataStatus { get; private set; }

    public event EventHandler? DataStatusChanged;

    /// <summary>出現時の一覧の受信と周りから外れたことは対象の変化を伴わないので、1秒ごとの読み直しでも見る。</summary>
    public void RefreshDataStatus()
    {
        var status = _target.CreateDataStatus(GameReceptionStateStore.Current);
        if (status == DataStatus)
        {
            return;
        }

        DataStatus = status;
        DataStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public long EntityId => _target.EntityId;

    public Zproto.EEntityType? EntityType => _target.EntityType;

    public long EntityUuid => _target.EntityUuid;

    public bool IsEntityAcquired => _target.IsAcquired;

    public bool IsWaitingForEntity => _target.IsWaitingForEntity;

    public EntityTargetInfo? Entity => _target.Entity;

    public string TargetName => _target.Name;

    public ReadOnlyObservableCollection<PlayerBuffEntry> Entries { get; }

    public WidgetListItemViewModel Widget => _widget;

    public string CardMenuText => LocalizationManager.Instance.GetString(
        _kind == PlayerBuffListKind.Debuff
            ? "BuffList_Menu_DebuffCard"
            : "BuffList_Menu_BuffCard");

    /// <summary>優先表示リストはプレイヤーから開いた一覧と同じウィジェットのもの。</summary>
    public string PriorityMenuText => BuffDebuffCardContent.GetPriorityMenuText(
        _kind == PlayerBuffListKind.Debuff,
        MenuEntry is { } entry && _widget.IsBuffListPriorityBuff(entry.BaseId));

    public void RefreshMenuText()
    {
        OnPropertyChanged(nameof(PriorityMenuText));
    }

    public string HideMenuText => LocalizationManager.Instance.GetString(
        _kind == PlayerBuffListKind.Debuff
            ? "BuffList_Menu_HideDebuff"
            : "BuffList_Menu_HideBuff");

    public string AddToGroupMenuText => LocalizationManager.Instance.GetString("BuffList_Menu_AddToGroup");

    /// <summary>バフ・デバフグループは自分のバフだけを出すので、エンティティの一覧からは足さない(項目を出さない)。</summary>
    public bool CanAddToGroup(PlayerBuffEntry? entry) => false;

    public string AddToShortcutsMenuText => LocalizationManager.Instance.GetString("BuffList_Menu_AddToBuffCardShortcuts");

    /// <summary>バフ・デバフカードのショートカットは自分のバフだけなので、エンティティの一覧からは足さない(項目を出さない)。</summary>
    public bool CanAddToShortcuts(PlayerBuffEntry? entry) => false;

    public bool RepresentsEntity(long entityUuid)
    {
        return _target.Represents(entityUuid);
    }

    [RelayCommand]
    private void OpenCard(PlayerBuffEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _openCard?.Invoke(_target, _kind, entry.CardKey, entry.BaseId);
    }

    [RelayCommand]
    private void TogglePriority(PlayerBuffEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _widget.SetBuffListPriorityBuffEnabledFromWidget(
            entry.BaseId,
            !_widget.IsBuffListPriorityBuff(entry.BaseId));
    }

    [RelayCommand]
    private void HideBuff(PlayerBuffEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _widget.HideBuffInList(entry.BaseId);
    }

    /// <summary>項目を出さないので呼ばれない(<see cref="CanAddToGroup"/>)。取り決めを満たすためだけにある。</summary>
    [RelayCommand(CanExecute = nameof(CanAddToGroup))]
    private void AddToGroup(PlayerBuffEntry? entry)
    {
    }

    /// <summary>項目を出さないので呼ばれない(<see cref="CanAddToShortcuts"/>)。取り決めを満たすためだけにある。</summary>
    [RelayCommand(CanExecute = nameof(CanAddToShortcuts))]
    private void AddToShortcuts(PlayerBuffEntry? entry)
    {
    }

    public bool TryApplyEntity(EntityTargetInfo entity)
    {
        return _target.TryApply(entity);
    }

    public void ReleaseEntity()
    {
        _target.Release();
    }

    public void RefreshPresentation()
    {
        HeaderText = WidgetHeaderText.Join(_widget.DisplayName, _target.Name);
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
        _target.Changed -= Target_Changed;
        _widget.BuffListSettingsChanged -= Widget_BuffListSettingsChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    /// <summary>
    /// 表示設定(保存かプレビュー)が変わった。色を作り直し、行も作り直す
    /// (優先表示と非表示のバフを効かせる。ゲージの長さもそこで当て直る)。
    /// </summary>
    private void Widget_BuffListSettingsChanged(object? sender, EventArgs e)
    {
        _settings = _widget.GetBuffListSettingsSnapshot();
        GaugeBrush = BuffGaugeBrush.Create(_settings.GaugeColors);
        Refresh();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
        RefreshDataStatus();
    }

    private void Target_Changed(object? sender, EventArgs e)
    {
        RefreshPresentation();
        Refresh();
        RefreshDataStatus();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CardMenuText));
        OnPropertyChanged(nameof(PriorityMenuText));
        OnPropertyChanged(nameof(HideMenuText));
        OnPropertyChanged(nameof(AddToGroupMenuText));
        OnPropertyChanged(nameof(AddToShortcutsMenuText));
        RefreshPresentation();
        Refresh();
    }

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        var snapshots = MeterSnapshotProvider.GetEntityBuffs(
            EntityUuid,
            _kind,
            WidgetConfigDefaults.GroupsSameBuffs(_settings, isEntityTarget: true));
        SynchronizeEntries(BuffListSnapshotFilter.Apply(_settings, snapshots));
    }

    private void SynchronizeEntries(IReadOnlyList<PlayerBuffSnapshot> snapshots)
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
            // 対象者名とレベルはバフ・デバフカードと同じく窓の対象のもの。
            var name = BuffListRowName.Format(_settings, snapshot, _target.Name, _target.Level);
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
