using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.ViewModels.Notifications;

/// <summary>
/// メイン窓の通知タブ。中央に項目の一覧(名前とスイッチ)、右に選んだ項目の文章と設定を出す。
///
/// <para>
/// 一覧は本体の項目の後ろに、動いているプラグインの項目をプラグインごとに並べる(<see cref="PluginNotificationRegistry"/>)。
/// プラグインを止めたらその項目を外す(保存した設定は残す)。
/// </para>
///
/// <para>
/// 保存前プレビューは持たず、変えたらその場で保存する(<see cref="ConfigManager.SaveNotifications"/>)。
/// 文章と出現の対象の名前は打ち終わってから <see cref="TextSaveDelay"/> で保存し、タブを離れたとき・アプリを閉じるとき・プラグインの項目を外すときは待たずに保存する(<see cref="FlushPendingSave"/>)。
/// Windows の通知を使うオンの項目が無くなったら、Windows の通知の登録を消す(<see cref="NotificationService.RemoveWindowsNotificationRegistration"/>)。
/// </para>
/// </summary>
public sealed partial class NotificationsViewModel : ObservableObject
{
    /// <summary>文章を打ち終わってから保存するまでの待ち。推移グラフのホイールの保存と同じ。</summary>
    private static readonly TimeSpan TextSaveDelay = TimeSpan.FromMilliseconds(300);

    private readonly DispatcherTimer _textSaveTimer;
    private readonly ObservableCollection<NotificationItemViewModel> _items;
    private readonly int _hostItemCount;
    private bool _usesWindowsNotification;

    [ObservableProperty]
    private NotificationItemViewModel? _selectedItem;

    public NotificationsViewModel()
    {
        _items =
        [
            new NotificationItemViewModel(
                NotificationKind.EntryConfirmation,
                "Notification_EntryConfirmation",
                [("Content", "Settings_PlayerListNotification_Field_Content", "{Content}")]),
            new NotificationItemViewModel(
                NotificationKind.HealthLow,
                "Settings_PlayerListNotification_HealthLow",
                [("PlayerName", "Settings_PlayerInfo_Field_Name", "{PlayerName}")]),
            new NotificationItemViewModel(
                NotificationKind.PlayerAppeared,
                "Notification_PlayerAppeared",
                [("PlayerName", "Settings_PlayerInfo_Field_Name", "{PlayerName}")]),
            new NotificationItemViewModel(
                NotificationKind.EntityAppeared,
                "Notification_EntityAppeared",
                [("EntityName", "Settings_EntityInfo_Field_Name", "{EntityName}")]),
            new NotificationItemViewModel(
                NotificationKind.Targeted,
                "Notification_Targeted",
                [
                    ("PlayerName", "Settings_PlayerInfo_Field_Name", "{PlayerName}"),
                    ("EntityName", "Settings_EntityInfo_Field_Name", "{EntityName}")
                ]),
            new NotificationItemViewModel(
                NotificationKind.BuffExpired,
                "Settings_BuffCardNotification_Expired",
                [
                    ("BuffName", "Settings_BuffInfo_Field_BuffName", "{BuffName}"),
                    ("TargetName", "Settings_BuffInfo_Field_Name", "{TargetName}"),
                    ("CasterName", "Settings_BuffInfo_Field_Caster", "{CasterName}")
                ]),
            new NotificationItemViewModel(
                NotificationKind.CuisinePotionLow,
                "Settings_BuffCardNotification_CuisinePotionLow",
                [
                    ("BuffName", "Settings_BuffInfo_Field_BuffName", "{BuffName}"),
                    ("TargetName", "Settings_BuffInfo_Field_Name", "{TargetName}")
                ]),
            new NotificationItemViewModel(
                NotificationKind.TelegraphedSkill,
                "Settings_TimelineNotification_TelegraphedSkill",
                [
                    ("EntityName", "Settings_EntityInfo_Field_Name", "{EntityName}"),
                    ("SkillName", "Metric_SkillName", "{SkillName}")
                ]),
            new NotificationItemViewModel(
                NotificationKind.EntityCast,
                "Notification_EntityCast",
                [("EntityName", "Settings_EntityInfo_Field_Name", "{EntityName}")]),
            new NotificationItemViewModel(
                NotificationKind.PrivateChat,
                "Notification_PrivateChat",
                [("PlayerName", "Settings_PlayerInfo_Field_Name", "{PlayerName}")]),
            new NotificationItemViewModel(
                NotificationKind.WorldChat,
                "Notification_WorldChat",
                [("PlayerName", "Settings_PlayerInfo_Field_Name", "{PlayerName}")])
        ];

        Items = new ReadOnlyObservableCollection<NotificationItemViewModel>(_items);
        _hostItemCount = _items.Count;

        var notifications = ConfigManager.Instance.GetNotificationsSnapshot();
        foreach (var item in _items)
        {
            item.Load(notifications.Get(item.Kind!.Value));
            Subscribe(item);
        }

        _usesWindowsNotification = notifications.AnyEnabled(item => item.IsWindowsNotification);
        _textSaveTimer = new DispatcherTimer
        {
            Interval = TextSaveDelay
        };
        _textSaveTimer.Tick += TextSaveTimer_Tick;

        _items[0].IsSelected = true;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        var registry = PluginNotificationRegistry.Instance;
        foreach (var group in registry.Groups)
        {
            AddPluginGroup(group);
        }

        registry.GroupAdded += Registry_GroupAdded;
        registry.GroupRemoved += Registry_GroupRemoved;
    }

    /// <summary>一覧の項目。本体の項目(並びは <see cref="NotificationKind"/> の並び)の後ろに、プラグインの項目(プラグインを入れた順、プラグインの中は定義の順)。</summary>
    public ReadOnlyObservableCollection<NotificationItemViewModel> Items { get; }

    /// <summary>読み上げの声を確かめる時機。画面が確かめてメッセージを出す(引数は確かめる項目)。</summary>
    public event EventHandler<NotificationItemViewModel>? SpeechVoiceCheckRequested;

    /// <summary>
    /// エンジンから話者の一覧を取り直して、全部の項目の選択肢を作り直す。失敗したら選択肢は今のままにし、結果の種類を返す
    /// (メッセージを出すかは画面が決める。出すのは読み上げで VOICEVOX を選んだときだけ)。
    /// </summary>
    public async Task<VoicevoxResultKind> RefreshVoicevoxSpeakersAsync()
    {
        var result = await VoicevoxClient.GetStylesAsync();
        if (result.Kind == VoicevoxResultKind.Success)
        {
            foreach (var item in Items)
            {
                item.ApplyFetchedVoicevoxStyles(result.Value!);
            }
        }

        return result.Kind;
    }

    /// <summary>待っている文章の保存があれば、待たずに保存する(タブを離れたとき・アプリを閉じるとき)。</summary>
    public void FlushPendingSave()
    {
        if (_textSaveTimer.IsEnabled)
        {
            Save();
        }
    }

    private void Item_Changed(object? sender, EventArgs e)
    {
        Save();
    }

    private void Item_TextChanged(object? sender, EventArgs e)
    {
        _textSaveTimer.Stop();
        _textSaveTimer.Start();
    }

    private void TextSaveTimer_Tick(object? sender, EventArgs e)
    {
        Save();
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NotificationItemViewModel.IsSelected)
            || sender is not NotificationItemViewModel { IsSelected: true } item)
        {
            return;
        }

        SelectedItem = item;
        foreach (var other in Items)
        {
            if (!ReferenceEquals(other, item))
            {
                other.IsSelected = false;
            }
        }
    }

    private void Save()
    {
        _textSaveTimer.Stop();

        // 一覧に無いプラグイン(止まっている・読めない)の項目の設定は、保存してある値のまま残す。
        var pluginItems = ConfigManager.Instance.GetNotificationsSnapshot().PluginItems;
        foreach (var item in Items.Where(item => item.PluginItemKey is not null))
        {
            pluginItems[item.PluginItemKey!] = item.CreatePluginConfig();
        }

        var notifications = new NotificationsConfig
        {
            Items = Items.Where(item => item.Kind is not null).ToDictionary(item => item.Kind!.Value, item => item.CreateConfig()),
            PluginItems = pluginItems
        };
        ConfigManager.Instance.SaveNotifications(notifications);

        // Windows の通知のためのアプリの名前とアイコンの登録は、Windows の通知を使うオンの項目が無くなったら
        // 通知センターのこのアプリの通知と一緒に消す(使うときに通知の処理が書く)。
        var usesWindowsNotification = notifications.AnyEnabled(item => item.IsWindowsNotification);
        if (_usesWindowsNotification && !usesWindowsNotification)
        {
            NotificationService.RemoveWindowsNotificationRegistration();
        }

        _usesWindowsNotification = usesWindowsNotification;
    }

    private void Subscribe(NotificationItemViewModel item)
    {
        item.Changed += Item_Changed;
        item.TextChanged += Item_TextChanged;
        item.PropertyChanged += Item_PropertyChanged;
        item.SpeechVoiceCheckRequested += Item_SpeechVoiceCheckRequested;
    }

    private void Unsubscribe(NotificationItemViewModel item)
    {
        item.Changed -= Item_Changed;
        item.TextChanged -= Item_TextChanged;
        item.PropertyChanged -= Item_PropertyChanged;
        item.SpeechVoiceCheckRequested -= Item_SpeechVoiceCheckRequested;
    }

    private void Registry_GroupAdded(object? sender, PluginNotificationGroup group)
    {
        AddPluginGroup(group);
    }

    /// <summary>
    /// 動き出したプラグインの項目を、保存した値で足す。置き場所は、本体の項目と、プラグインの一覧で前にあるプラグインの項目の後ろ。
    /// </summary>
    private void AddPluginGroup(PluginNotificationGroup group)
    {
        var index = _hostItemCount;
        foreach (var other in PluginNotificationRegistry.Instance.Groups)
        {
            if (ReferenceEquals(other, group))
            {
                break;
            }

            index += Items.Count(item => string.Equals(item.PluginKey, other.PluginKey, StringComparison.OrdinalIgnoreCase));
        }

        var notifications = ConfigManager.Instance.GetNotificationsSnapshot();
        for (var i = 0; i < group.Definitions.Count; i++)
        {
            var item = new NotificationItemViewModel(group, group.Definitions[i], isFirstInGroup: i == 0);
            notifications.PluginItems.TryGetValue(item.PluginItemKey!, out var saved);
            item.LoadPlugin(saved);
            Subscribe(item);
            _items.Insert(index + i, item);
        }
    }

    /// <summary>止めたプラグインの項目を外す。待っている文章は先に保存する。選んでいた項目なら、一覧の先頭を選ぶ。</summary>
    private void Registry_GroupRemoved(object? sender, PluginNotificationGroup group)
    {
        FlushPendingSave();

        var removed = Items.Where(item => string.Equals(item.PluginKey, group.PluginKey, StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var item in removed)
        {
            Unsubscribe(item);
            _items.Remove(item);
        }

        if (SelectedItem is not null && removed.Contains(SelectedItem))
        {
            _items[0].IsSelected = true;
        }
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in Items)
        {
            item.RefreshLocalizedText();
        }
    }

    private void Item_SpeechVoiceCheckRequested(object? sender, EventArgs e)
    {
        if (sender is NotificationItemViewModel item)
        {
            SpeechVoiceCheckRequested?.Invoke(this, item);
        }
    }
}
