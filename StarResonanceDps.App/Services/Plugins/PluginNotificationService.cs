// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Notifications;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグイン1つに渡す通知の口。項目の定義は初期化の後に受け取る(<see cref="Attach"/>、<see cref="PluginNotificationRegistry"/> が呼ぶ)。
///
/// <para>
/// オンとオフと項目の設定の値は、保存した値から作った写しを持ち、どのスレッドからでも読めるようにする。
/// 写しは通知タブの値が保存されるたびに作り直し(<see cref="UpdateFromConfig"/>、画面のスレッド)、変わった項目を <see cref="SettingsChanged"/> で知らせる。
/// 通知は画面のスレッドへ回し、出す時点の保存した値(スイッチ・文章・出し方)で出す(<see cref="NotificationService"/>)。
/// </para>
/// </summary>
internal sealed class PluginNotificationService : IPluginNotificationService
{
    private static readonly IReadOnlyDictionary<string, ItemState> NoItems =
        new Dictionary<string, ItemState>(StringComparer.OrdinalIgnoreCase);

    private readonly PluginCallGuard _guard;
    private readonly string _pluginKey;
    private IReadOnlyDictionary<string, ItemState> _items = NoItems;
    private volatile bool _isDetached;

    public PluginNotificationService(PluginCallGuard guard, string pluginKey)
    {
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _pluginKey = pluginKey;
    }

    public event EventHandler<string>? SettingsChanged;

    public bool IsEnabled(string id)
    {
        return GetItem(id).IsEnabled;
    }

    public PluginSettingValues GetSettings(string id)
    {
        return GetItem(id).Settings;
    }

    public void Notify(string id, IReadOnlyDictionary<string, string>? values = null)
    {
        var item = GetItem(id);
        var copiedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values ?? PluginTexts.Empty)
        {
            if (key is null || !item.Definition.Fields.Any(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"'{key}' is not a field of the notification '{item.Definition.Id}'.", nameof(values));
            }

            copiedValues[key] = value ?? string.Empty;
        }

        if (_isDetached || Application.Current?.Dispatcher is not { HasShutdownStarted: false } dispatcher)
        {
            return;
        }

        dispatcher.BeginInvoke(() => Show(item, copiedValues));
    }

    /// <summary>検証した定義を受け取り、保存した値から写しを作る(知らせは出さない)。画面のスレッドで呼ぶ。</summary>
    public void Attach(IReadOnlyList<PluginNotificationDefinition> definitions, NotificationsConfig notifications)
    {
        var items = new Dictionary<string, ItemState>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            items[definition.Id] = CreateState(definition, notifications);
        }

        Volatile.Write(ref _items, items);
    }

    /// <summary>通知タブの値が保存された。写しを作り直し、オンとオフか項目の設定が変わった項目を知らせる。画面のスレッドで呼ぶ。</summary>
    public void UpdateFromConfig(NotificationsConfig notifications)
    {
        if (_isDetached)
        {
            return;
        }

        var previous = Volatile.Read(ref _items);
        var items = new Dictionary<string, ItemState>(StringComparer.OrdinalIgnoreCase);
        var changedIds = new List<string>();
        foreach (var (id, item) in previous)
        {
            var updated = CreateState(item.Definition, notifications);
            items[id] = updated;
            if (!item.HasSameSettings(updated))
            {
                changedIds.Add(id);
            }
        }

        Volatile.Write(ref _items, items);
        foreach (var id in changedIds)
        {
            if (_isDetached)
            {
                return;
            }

            _guard.TryInvoke("Notification settings notification", () => SettingsChanged?.Invoke(this, id));
        }
    }

    /// <summary>プラグインを止めた・終えた。待っている通知を捨て、知らせも出さない。</summary>
    public void Detach()
    {
        _isDetached = true;
    }

    private ItemState GetItem(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Volatile.Read(ref _items).TryGetValue(id, out var item)
            ? item
            : throw new ArgumentException(
                $"The notification '{id}' is not defined, or the definitions have not been read yet (they are read after Initialize returns).",
                nameof(id));
    }

    private ItemState CreateState(PluginNotificationDefinition definition, NotificationsConfig notifications)
    {
        var itemKey = NotificationsConfig.GetPluginItemKey(_pluginKey, definition.Id);
        notifications.PluginItems.TryGetValue(itemKey, out var saved);
        var values = PluginSettingValueCodec.ReadAll(definition.Settings, saved?.Settings);
        return new ItemState(definition, itemKey, saved?.Item.IsEnabled ?? false, values, new PluginSettingValues(values));
    }

    /// <summary>出す時点の保存した値で出す。オフなら出さない。</summary>
    private void Show(ItemState item, IReadOnlyDictionary<string, string> values)
    {
        if (_isDetached || ConfigManager.Instance.GetPluginNotificationItem(item.ItemKey) is not { Item.IsEnabled: true } saved)
        {
            return;
        }

        var format = saved.Item.FormatString
            ?? PluginNotificationTextFormatter.GetDefaultText(item.Definition, LocalizationManager.Instance.CurrentCulture);
        NotificationService.Instance.Notify(saved.Item, PluginNotificationTextFormatter.Format(item.Definition, format, values));
    }

    private sealed record ItemState(
        PluginNotificationDefinition Definition,
        string ItemKey,
        bool IsEnabled,
        IReadOnlyDictionary<string, object> Values,
        PluginSettingValues Settings)
    {
        public bool HasSameSettings(ItemState other)
        {
            return IsEnabled == other.IsEnabled
                && Values.Count == other.Values.Count
                && Values.All(pair => other.Values.TryGetValue(pair.Key, out var value) && Equals(pair.Value, value));
        }
    }
}
