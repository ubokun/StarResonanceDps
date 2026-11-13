// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Notifications;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// バフ・デバフグループ。自分のバフ・デバフのうち、設定の「バフ・デバフ」でオンにした行だけを、設定の並びで出す。
///
/// <para>
/// 付いていない行も出す(絵と名前はそのまま、ゲージなし、時間の欄は「効果なし」)。同じ番号のバフは1行にまとめ(カードと同じ読み方)、
/// 付与者名は出さない。料理・薬剤はまとまり1行で、名前はまとまりの名前。自分の実体がまだ分からない間は何も出さない。
/// 行の部品は鍵ごとに使い回し、1秒ごとに値だけを入れ替える。
/// </para>
///
/// <para>
/// 行の左クリックはその行のカードを開き、右クリックはメニュー(カード・グループから削除・通知対象にする/しない)を開く。行を掴んで動かすと並べ替えになる
/// (<see cref="IRowReorderTarget"/>。オフの行も含めた全体の並びの中で動かし、確定はステータス詳細の窓と同じ扱い)。
/// バフかデバフかは一覧の振り分けと同じ表の種別で決める(<see cref="CombatDataCatalog.GetBuffListKind"/>)。
/// </para>
///
/// <para>
/// オンの行のバフは、窓を開いている間、通知(バフ・デバフ効果切れ・薬剤・料理効果切れ)の対象になる(<see cref="BuffNotificationRegistry"/>)。
/// バフ・デバフ効果切れは、そのうちグループの設定の通知対象に入っている行だけ。
/// </para>
/// </summary>
public sealed partial class BuffDebuffGroupWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable, IRowReorderTarget
{
    private readonly ObservableCollection<PlayerBuffEntry> _entries = [];
    private readonly Dictionary<BuffEntryKey, PlayerBuffEntry> _entriesByKey = [];
    private readonly Dictionary<PlayerBuffEntry, BuffEntryKey> _keysByEntry = [];

    /// <summary>行をクリックしたときにバフ・デバフカードを開く経路。</summary>
    private readonly Action<long, PlayerBuffListKind, BuffEntryKey>? _openCard;

    private readonly DispatcherTimer _refreshTimer;
    private BuffDebuffGroupWidgetSettingsConfig _settings;

    /// <summary>
    /// 通知(バフ・デバフ効果切れ・薬剤・料理効果切れ)の判定。オンの行ごとに、窓を開いている間だけ借りる(同じバフを見ているカードと共有)。
    /// </summary>
    private readonly Dictionary<BuffEntryKey, BuffNotificationWatch> _notificationWatches = [];

    private PlayerRosterEntry? _player;

    /// <summary>行を掴んでいる間の、掴んだ行の鍵と見た目だけの全体の並び。掴んでいなければ null。</summary>
    private BuffEntryKey? _reorderKey;
    private List<BuffEntryKey>? _previewOrder;

    private bool _isDisposed;

    /// <summary>バフの行のゲージの塗り(「バフカラー」)。設定が変わったら作り直す。</summary>
    [ObservableProperty]
    private Brush _buffGaugeFill = Brushes.Transparent;

    /// <summary>デバフの行のゲージの塗り(「デバフカラー」)。設定が変わったら作り直す。</summary>
    [ObservableProperty]
    private Brush _debuffGaugeFill = Brushes.Transparent;

    /// <summary>右クリックのメニューを開いた行。メニューの文言(バフかデバフか)はこの行で決める。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardMenuText))]
    [NotifyPropertyChangedFor(nameof(RemoveMenuText))]
    [NotifyPropertyChangedFor(nameof(NotificationTargetMenuText))]
    private PlayerBuffEntry? _menuEntry;

    public BuffDebuffGroupWidgetViewModel(
        WidgetListItemViewModel groupWidget,
        PlayerRosterEntry? initialPlayer,
        Action<long, PlayerBuffListKind, BuffEntryKey>? openCard = null)
        : base(groupWidget, requestedCharacterId: null)
    {
        _openCard = openCard;
        _settings = groupWidget.GetBuffDebuffGroupSettingsSnapshot();
        CreateGaugeBrushes();
        Entries = new ReadOnlyObservableCollection<PlayerBuffEntry>(_entries);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        groupWidget.BuffDebuffGroupSettingsChanged += Widget_BuffDebuffGroupSettingsChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        InitializePlayer(initialPlayer);
        Refresh();
        _refreshTimer.Start();
    }

    public ReadOnlyObservableCollection<PlayerBuffEntry> Entries { get; }

    /// <summary>自分だけを出す1枚の窓なので、タイトルはウィジェット名だけ。</summary>
    protected override bool ShowsPlayerIdentityInHeader => false;

    /// <summary>メニューの色を引くウィジェット。</summary>
    public WidgetListItemViewModel Widget => PlayerWidget;

    /// <summary>メニューの1つ目の項目。バフの行なら「バフカード」、デバフの行なら「デバフカード」。</summary>
    public string CardMenuText => LocalizationManager.Instance.GetString(
        MenuEntry?.IsDebuff == true
            ? "BuffList_Menu_DebuffCard"
            : "BuffList_Menu_BuffCard");

    /// <summary>メニューの2つ目の項目。バフの行なら「このバフをグループから削除」、デバフの行なら「このデバフをグループから削除」。</summary>
    public string RemoveMenuText => LocalizationManager.Instance.GetString(
        MenuEntry?.IsDebuff == true
            ? "BuffDebuffGroup_Menu_RemoveDebuff"
            : "BuffDebuffGroup_Menu_RemoveBuff");

    /// <summary>
    /// メニューの3つ目の項目「このバフを通知対象にする」「〜しない」(デバフの行は「このデバフを」)。開いた行の今の通知対象の状態で決まる。
    /// </summary>
    public string NotificationTargetMenuText => BuffDebuffCardContent.GetNotificationTargetMenuText(
        MenuEntry?.IsDebuff == true,
        TryGetKey(MenuEntry, out var key) && IsNotificationTarget(key));

    /// <summary>メニューを開く直前に、通知対象の項目の文言を今の状態にする(同じ行を開き直すと <see cref="MenuEntry"/> が変わらないため)。</summary>
    public void RefreshMenuText()
    {
        OnPropertyChanged(nameof(NotificationTargetMenuText));
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
        PlayerWidget.BuffDebuffGroupSettingsChanged -= Widget_BuffDebuffGroupSettingsChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        ReleaseNotificationWatches();
    }

    /// <summary>行からカードを開けるか。自分が分かっていて、行がバフかデバフに振り分けられるとき。</summary>
    public bool CanOpenCard(PlayerBuffEntry? entry)
    {
        return SelectedCharacterId is not null
            && _openCard is not null
            && TryGetKey(entry, out var key)
            && ResolveListKind(key) is not null;
    }

    /// <summary>行の鍵(並べ替えの単位も行1つずつ)。</summary>
    public object? GetRowReorderKey(object item)
    {
        return item is PlayerBuffEntry entry && _keysByEntry.TryGetValue(entry, out var key) ? key : null;
    }

    /// <summary>行を掴み始めた。離すまで、1秒ごとの行の当て直しを止める(当て直しで行が動くと、掴んでいる行の位置が変わる)。</summary>
    public void BeginRowReorder(object key)
    {
        if (key is not BuffEntryKey entryKey)
        {
            return;
        }

        _reorderKey = entryKey;
        _previewOrder = GetSettingsOrder();
    }

    /// <summary>
    /// 掴んでいる間の見た目だけの並び。<b>オフの行も含めた全体の並び</b>の中で、掴んだ行を <paramref name="beforeKey"/> の行の前
    /// (null なら末尾)へ動かし、行を当て直す(行の部品は作り直さない)。
    /// </summary>
    public void ShowRowReorderAt(object? beforeKey)
    {
        if (_reorderKey is not { } reorderKey || Equals(beforeKey, reorderKey))
        {
            return;
        }

        var order = GetSettingsOrder();
        if (!order.Remove(reorderKey))
        {
            return;
        }

        var insertAt = beforeKey is BuffEntryKey before ? order.IndexOf(before) : order.Count;
        if (insertAt < 0)
        {
            return;
        }

        order.Insert(insertAt, reorderKey);
        if (_previewOrder is not null && order.SequenceEqual(_previewOrder))
        {
            return;
        }

        _previewOrder = order;
        ApplyRows();
    }

    /// <summary>掴んでいる間に、掴んだ行を元の位置へ戻す(窓の外にいる間)。</summary>
    public void ShowRowReorderAtOriginalPosition()
    {
        if (_reorderKey is null)
        {
            return;
        }

        _previewOrder = GetSettingsOrder();
        ApplyRows();
    }

    /// <summary>
    /// 掴み終わり。<paramref name="commit"/> なら見た目の並びを確定する: 設定の窓が開いていなければその場で保存し、
    /// 開いていれば保存前の並びとして設定の窓へ渡す(<see cref="WidgetListItemViewModel.ChangeBuffDebuffGroupEntryOrderFromWidget"/>)。
    /// 止めていた行の当て直しはここで1回だけ反映する。
    /// </summary>
    public void EndRowReorder(bool commit)
    {
        if (_reorderKey is null)
        {
            return;
        }

        var previewOrder = _previewOrder;
        _reorderKey = null;
        _previewOrder = null;

        if (commit && previewOrder is not null && !previewOrder.SequenceEqual(GetSettingsOrder()))
        {
            PlayerWidget.ChangeBuffDebuffGroupEntryOrderFromWidget(previewOrder);
        }

        ApplyRows();
    }

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        _player = player;
        Refresh();
    }

    protected override WidgetDataStatus CreateDataStatus(GameReceptionSnapshot reception, PlayerRosterEntry? player)
        => WidgetDataStatusRules.ForBuffDebuffGroup(reception);

    [RelayCommand]
    private void OpenCard(PlayerBuffEntry? entry)
    {
        if (!CanOpenCard(entry)
            || SelectedCharacterId is not { } characterId
            || !TryGetKey(entry, out var key)
            || ResolveListKind(key) is not { } kind)
        {
            return;
        }

        _openCard!.Invoke(characterId, kind, key);
    }

    /// <summary>行をグループから外す(既定の行はオフにして残し、足した行は消す。決まりは <see cref="WidgetConfigDefaults.SetBuffEntryEnabled"/>)。</summary>
    [RelayCommand]
    private void RemoveFromGroup(PlayerBuffEntry? entry)
    {
        if (TryGetKey(entry, out var key))
        {
            PlayerWidget.SetBuffDebuffGroupEntryEnabledFromWidget(key, isEnabled: false);
        }
    }

    /// <summary>行のバフをグループの設定の通知対象に入れる・外す(今の状態の逆。決まりは <see cref="WidgetConfigDefaults.SetBuffEntryEnabled"/>)。</summary>
    [RelayCommand]
    private void ToggleNotificationTarget(PlayerBuffEntry? entry)
    {
        if (TryGetKey(entry, out var key))
        {
            PlayerWidget.SetBuffDebuffGroupNotificationTargetEnabledFromWidget(key, !IsNotificationTarget(key));
        }
    }

    /// <summary>行のバフがグループの設定の通知対象(設定の窓のプレビューを含む今の値)に入っているか。</summary>
    private bool IsNotificationTarget(BuffEntryKey key)
    {
        return _settings.NotificationTargets is { } targets
            && targets.Exists(target => target.IsEnabled && target.GetKey() == key);
    }

    /// <summary>設定(保存かプレビュー)が変わった。色を作り直し、行も当て直す(行のオン/オフ・並び・ゲージの長さ)。</summary>
    private void Widget_BuffDebuffGroupSettingsChanged(object? sender, EventArgs e)
    {
        _settings = PlayerWidget.GetBuffDebuffGroupSettingsSnapshot();
        CreateGaugeBrushes();
        Refresh();
    }

    private void CreateGaugeBrushes()
    {
        BuffGaugeFill = BuffGaugeBrush.Create(_settings.BuffGaugeColors!);
        DebuffGaugeFill = BuffGaugeBrush.Create(_settings.DebuffGaugeColors!);
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CardMenuText));
        OnPropertyChanged(nameof(RemoveMenuText));
        RefreshMenuText();
        Refresh();
    }

    /// <summary>
    /// 通知の判定を進め、行を当て直す。行を掴んでいる間は行の当て直しだけ止める(離したときに <see cref="EndRowReorder"/> が必ず当て直す)。
    /// 通知の判定は掴んでいる間も止めない(止めると、その間に消えたバフの効果切れを見落とす)。
    /// </summary>
    private void Refresh()
    {
        ObserveNotifications();
        if (_reorderKey is not null)
        {
            return;
        }

        ApplyRows();
    }

    /// <summary>今の値で行を作り、並びどおりに当て直す。掴んでいる間は見た目だけの並びで並べる。</summary>
    private void ApplyRows()
    {
        if (_isDisposed)
        {
            return;
        }

        // 自分の実体がまだ分からない間は、付いていない行も出さない(「効果なし」と分からないを分ける)。
        // カードと同じく、同じ番号のバフは残り時間の一番長い実体の1行で読む。
        if (SelectedCharacterId is not { } characterId
            || !MeterSnapshotProvider.TryGetPlayerBuffs(characterId, PlayerBuffListKind.Buff, groupSameBuffs: true, out var buffs)
            || !MeterSnapshotProvider.TryGetPlayerBuffs(characterId, PlayerBuffListKind.Debuff, groupSameBuffs: true, out var debuffs))
        {
            SynchronizeEntries([]);
            return;
        }

        var enabledKeys = (_settings.Entries ?? [])
            .Where(setting => setting.IsEnabled)
            .Select(setting => setting.GetKey())
            .ToHashSet();
        var inactiveText = LocalizationManager.Instance.GetString("Widget_BuffDebuffGroup_NoEffect");
        var rows = new List<PlayerBuffEntry>();
        foreach (var key in _previewOrder ?? GetSettingsOrder())
        {
            if (!enabledKeys.Contains(key))
            {
                continue;
            }

            var kind = ResolveListKind(key);
            var snapshot = kind switch
            {
                PlayerBuffListKind.Buff => Find(buffs, key),
                PlayerBuffListKind.Debuff => Find(debuffs, key),
                _ => null
            };

            rows.Add(UpdateEntry(key, snapshot, kind == PlayerBuffListKind.Debuff, inactiveText));
        }

        SynchronizeEntries(rows);
    }

    /// <summary>
    /// オンの行のバフを、行ごとの通知の判定へ渡す。読み方は行と同じ(同じ番号は残り時間の一番長い実体、料理・薬剤はまとまり)。
    /// バフ・デバフ効果切れを出すのは、グループの設定の通知対象に入っている行だけ(入っていない行は薬剤・料理効果切れだけを見る借り方)。
    /// 自分が分からない間・バフかデバフに振り分けられない行は判定を返す。オフにした行の判定も返す。
    /// </summary>
    private void ObserveNotifications()
    {
        if (_isDisposed)
        {
            return;
        }

        if (SelectedCharacterId is not { } characterId
            || !MeterSnapshotProvider.TryGetPlayerBuffs(characterId, PlayerBuffListKind.Buff, groupSameBuffs: true, out var buffs)
            || !MeterSnapshotProvider.TryGetPlayerBuffs(characterId, PlayerBuffListKind.Debuff, groupSameBuffs: true, out var debuffs))
        {
            ReleaseNotificationWatches();
            return;
        }

        var target = BuffNotificationTarget.ForPlayer(characterId);
        var targetName = GetTargetName(characterId);
        var observedKeys = new HashSet<BuffEntryKey>();
        foreach (var setting in _settings.Entries ?? [])
        {
            if (!setting.IsEnabled)
            {
                continue;
            }

            var key = setting.GetKey();
            var snapshots = ResolveListKind(key) switch
            {
                PlayerBuffListKind.Buff => buffs,
                PlayerBuffListKind.Debuff => debuffs,
                _ => null
            };
            if (snapshots is null || !observedKeys.Add(key))
            {
                continue;
            }

            var notifiesExpiry = IsNotificationTarget(key);
            _notificationWatches.TryGetValue(key, out BuffNotificationWatch? watch);
            if (watch is null || watch.Target != target || watch.NotifiesExpiry != notifiesExpiry)
            {
                // 先に借りてから返す(同じ対象とバフのまま借り方だけ変えるとき、判定の控えを捨てない)。
                var previous = watch;
                watch = BuffNotificationRegistry.Instance.Watch(
                    target,
                    BuffNotificationRegistry.CreateBuffKey(
                        key.Group,
                        key.IsGroup ? string.Empty : MeterSnapshotProvider.CreateBuffCardKey(key.BuffId)),
                    notifiesExpiry);
                previous?.Dispose();
                _notificationWatches[key] = watch;
            }

            watch.Observe(Find(snapshots, key), targetName);
        }

        foreach (var key in _notificationWatches.Keys.Where(key => !observedKeys.Contains(key)).ToArray())
        {
            _notificationWatches[key].Dispose();
            _notificationWatches.Remove(key);
        }
    }

    private void ReleaseNotificationWatches()
    {
        foreach (var watch in _notificationWatches.Values)
        {
            watch.Dispose();
        }

        _notificationWatches.Clear();
    }

    /// <summary>通知に入れる名前。バフ・デバフカードと同じ規則(プレイヤー一覧・メーターと同じ)。</summary>
    private string GetTargetName(long characterId)
    {
        var playerIdentity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        return PlayerInfoFormatFormatter.GetDisplayName(
            _player?.Name ?? playerIdentity?.Name,
            playerIdentity?.UserId ?? characterId,
            _player?.IsSelf ?? (AppState.PlayerUID != 0 && characterId == AppState.PlayerUID),
            _player?.IsNpc ?? playerIdentity?.IsNpc ?? false,
            _player?.ProfessionId ?? playerIdentity?.ProfessionId ?? 0,
            PlayerRosterPresentationStore.Instance.NameDisplayMode);
    }

    /// <summary>設定の行の全体の並び(オフの行も含む)。</summary>
    private List<BuffEntryKey> GetSettingsOrder()
    {
        return [.. (_settings.Entries ?? []).Select(setting => setting.GetKey())];
    }

    /// <summary>行がバフリストとデバフリストのどちらに出るか(一覧の振り分けと同じ表の種別)。</summary>
    private static PlayerBuffListKind? ResolveListKind(BuffEntryKey key)
    {
        return key.IsGroup
            ? CombatDataCatalog.GetBuffGroupListKind(key.Group)
            : CombatDataCatalog.GetBuffListKind(key.BuffId);
    }

    private bool TryGetKey(PlayerBuffEntry? entry, out BuffEntryKey key)
    {
        key = default;
        return entry is not null && _keysByEntry.TryGetValue(entry, out key);
    }

    /// <summary>鍵の行に出すバフ。まとまりはカードと同じ選び方(<see cref="BuffDebuffCardContent.FindByGroup"/>)。</summary>
    private static PlayerBuffSnapshot? Find(IReadOnlyList<PlayerBuffSnapshot> snapshots, BuffEntryKey key)
    {
        if (key.IsGroup)
        {
            return BuffDebuffCardContent.FindByGroup(snapshots, key.Group);
        }

        foreach (var snapshot in snapshots)
        {
            if (snapshot.IsCardRepresentative && snapshot.BaseId == key.BuffId)
            {
                return snapshot;
            }
        }

        return null;
    }

    /// <summary>鍵の行の部品を今の値にする。無ければ作る。名前は、まとまりならまとまりの名前、バフならバフ名(付与者名は出さない)。</summary>
    private PlayerBuffEntry UpdateEntry(BuffEntryKey key, PlayerBuffSnapshot? snapshot, bool isDebuff, string inactiveText)
    {
        _entriesByKey.TryGetValue(key, out var entry);

        if (snapshot is not null)
        {
            var name = key.IsGroup
                ? CombatDataCatalog.AppendBuffInternalId(BuffDebuffCardContent.GetGroupName(key.Group), snapshot.BaseId)
                : snapshot.Name;
            if (entry is null)
            {
                entry = new PlayerBuffEntry(snapshot, name);
            }
            else
            {
                entry.Update(snapshot, name);
            }
        }
        else
        {
            var name = key.IsGroup
                ? BuffDebuffCardContent.GetGroupName(key.Group)
                : CombatDataCatalog.GetBuffDisplayName(key.BuffId, SourceLanding.None);
            var iconName = key.IsGroup
                ? CombatDataCatalog.GetBuffGroupIconName(key.Group)
                : CombatDataCatalog.GetBuffOwnIconName(key.BuffId);
            if (entry is null)
            {
                entry = PlayerBuffEntry.CreateInactive(name, iconName, inactiveText);
            }
            else
            {
                entry.UpdateInactive(name, iconName, inactiveText);
            }
        }

        entry.IsDebuff = isDebuff;
        _entriesByKey[key] = entry;
        _keysByEntry[entry] = key;
        return entry;
    }

    /// <summary>行を <paramref name="rows"/> の並びにする。行の部品は作り直さず、増えた分だけ足し、減った分だけ外す。</summary>
    private void SynchronizeEntries(IReadOnlyList<PlayerBuffEntry> rows)
    {
        var kept = rows.ToHashSet();
        foreach (var (key, entry) in _entriesByKey.Where(pair => !kept.Contains(pair.Value)).ToList())
        {
            _entriesByKey.Remove(key);
            _keysByEntry.Remove(entry);
        }

        for (var index = _entries.Count - 1; index >= 0; index--)
        {
            if (kept.Contains(_entries[index]))
            {
                continue;
            }

            // 行が消えるならメニューも閉じる(消えた行に開いたまま残らないように)。
            _entries[index].IsMenuOpen = false;
            _entries.RemoveAt(index);
        }

        CollectionOrder.Apply(_entries, rows);

        // 行を入れ替えた後にまとめて計算する。作った直後の行にも同じ長さが当たる。
        var gaugeLengthSeconds = BuffGaugeBrush.GetLengthSeconds(_settings.GaugeLengthIndex);
        foreach (var entry in _entries)
        {
            entry.UpdateBar(gaugeLengthSeconds);
        }
    }
}
