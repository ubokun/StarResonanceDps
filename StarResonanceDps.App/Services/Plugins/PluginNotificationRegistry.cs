// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Config;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>通知タブに並べる、動いているプラグイン1つの通知の項目。</summary>
public sealed class PluginNotificationGroup
{
    internal PluginNotificationGroup(PluginInfo info, IReadOnlyList<PluginNotificationDefinition> definitions)
    {
        Info = info;
        Definitions = definitions;
    }

    public PluginInfo Info { get; }

    public string PluginKey => Info.Key;

    /// <summary>検証した定義の写し(並びはプラグインが返した順)。</summary>
    public IReadOnlyList<PluginNotificationDefinition> Definitions { get; }
}

/// <summary>
/// 動いているプラグインの通知の項目(全部のプラグインで1つ、画面のスレッドで使う)。
/// 通知タブは <see cref="Groups"/> と知らせで項目を足し外しする。止めたプラグインの項目は外す(保存した設定は残す)。
/// 通知タブの値が保存されたら、各プラグインの口(<see cref="PluginNotificationService"/>)の写しを作り直す。
/// </summary>
internal sealed class PluginNotificationRegistry
{
    private readonly List<PluginNotificationGroup> _groups = [];
    private readonly Dictionary<string, PluginNotificationService> _services = new(StringComparer.OrdinalIgnoreCase);

    private PluginNotificationRegistry()
    {
        ConfigManager.Instance.NotificationsChanged += ConfigManager_NotificationsChanged;
    }

    public static PluginNotificationRegistry Instance { get; } = new();

    /// <summary>項目のあるプラグイン。並びは足した順(プラグインの一覧と同じ)。</summary>
    public IReadOnlyList<PluginNotificationGroup> Groups => _groups;

    /// <summary>プラグインの項目を足した。値は足したプラグイン。</summary>
    public event EventHandler<PluginNotificationGroup>? GroupAdded;

    /// <summary>プラグインを止めたので、その項目を外した。</summary>
    public event EventHandler<PluginNotificationGroup>? GroupRemoved;

    /// <summary>初期化できたプラグインの、検証した定義を受け取る。項目が1つも無ければ通知タブには何も足さない。</summary>
    public void Register(PluginInfo info, IReadOnlyList<PluginNotificationDefinition> definitions, PluginNotificationService service)
    {
        service.Attach(definitions, ConfigManager.Instance.GetNotificationsSnapshot());
        _services[info.Key] = service;
        if (definitions.Count == 0)
        {
            return;
        }

        var group = new PluginNotificationGroup(info, definitions);
        _groups.Add(group);
        GroupAdded?.Invoke(this, group);
    }

    /// <summary>プラグインを止めた。通知タブからその項目を外す。</summary>
    public void Unregister(string pluginKey)
    {
        _services.Remove(pluginKey);
        var index = _groups.FindIndex(group => string.Equals(group.PluginKey, pluginKey, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        var group = _groups[index];
        _groups.RemoveAt(index);
        GroupRemoved?.Invoke(this, group);
    }

    private void ConfigManager_NotificationsChanged(object? sender, EventArgs e)
    {
        // 知らせを受けたプラグインの例外で止める(項目を外す)ことがあるので、写しで回す。
        var notifications = ConfigManager.Instance.GetNotificationsSnapshot();
        foreach (var service in _services.Values.ToArray())
        {
            service.UpdateFromConfig(notifications);
        }
    }
}
