// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.ViewModels;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 開いているプラグインのウィジェットの窓1つに渡す口。設定はカード(<see cref="WidgetListItemViewModel"/>)が持つ今の値(設定の窓のプレビューを含む)から作る。
/// 値はどのスレッドからでも読める写しにし、カードの知らせのたびに作り直して <see cref="SettingsChanged"/> を受け止めの中で出す。窓を閉じたら外す。
/// </summary>
internal sealed class PluginWidgetContext : IPluginWidgetContext
{
    private readonly PluginWidgetRegistration _registration;
    private readonly WidgetListItemViewModel _widget;
    private readonly PluginCallGuard _guard;
    private PluginSettingValues _settings;
    private bool _isDetached;

    public PluginWidgetContext(
        string widgetId,
        PluginWidgetRegistration registration,
        WidgetListItemViewModel widget,
        PluginCallGuard guard)
    {
        WidgetId = widgetId;
        _registration = registration;
        _widget = widget;
        _guard = guard;
        _settings = CreateSettings();
        _widget.PluginSettingsChanged += Widget_PluginSettingsChanged;
    }

    public string WidgetId { get; }

    public PluginSettingValues Settings => Volatile.Read(ref _settings);

    public event EventHandler? SettingsChanged;

    /// <summary>窓を閉じた(作るのに失敗した)。カードの知らせから外し、知らせも出さない。</summary>
    public void Detach()
    {
        if (_isDetached)
        {
            return;
        }

        _isDetached = true;
        _widget.PluginSettingsChanged -= Widget_PluginSettingsChanged;
    }

    private PluginSettingValues CreateSettings()
    {
        return new PluginSettingValues(PluginSettingValueCodec.ReadAll(
            _registration.Settings,
            _widget.GetPluginSettingsSnapshot().Values));
    }

    private void Widget_PluginSettingsChanged(object? sender, EventArgs e)
    {
        Volatile.Write(ref _settings, CreateSettings());
        _guard.TryInvoke("Widget settings notification", () => SettingsChanged?.Invoke(this, EventArgs.Empty));
    }
}
