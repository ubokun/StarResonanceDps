// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.ViewModels.Plugins;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグインが足したウィジェット1つ(カード1枚)。定義は検証した写し。
/// 窓が1枚だけのウィジェット(<see cref="IPluginWidgetProvider"/>)か、相手ごとに窓を開くウィジェット(<see cref="IPluginMultiWidgetProvider"/>)のどちらか。
/// </summary>
public sealed class PluginWidgetRegistration
{
    private readonly PluginCallGuard _guard;
    private readonly IPluginWidgetProvider? _provider;
    private readonly IPluginMultiWidgetProvider? _multiProvider;

    internal PluginWidgetRegistration(
        PluginInfo info,
        PluginWidgetDefinition definition,
        PluginCallGuard guard,
        IPluginWidgetProvider? provider,
        IPluginMultiWidgetProvider? multiProvider)
    {
        Info = info;
        Definition = definition;
        Key = WidgetKey.ForPlugin(info.Key, definition.Id);
        _guard = guard;
        _provider = provider;
        _multiProvider = multiProvider;
    }

    public PluginInfo Info { get; }

    public PluginWidgetDefinition Definition { get; }

    public WidgetKey Key { get; }

    /// <summary>相手ごとに窓を開くウィジェットか。</summary>
    public bool IsMultiWindow => _multiProvider is not null;

    /// <summary>プラグインを止めた。カードが一覧から外れるまでの間も、プラグインのコードを呼ばない。</summary>
    public bool IsStopped { get; private set; }

    /// <summary>全部の節の設定(節の順、節の中の順)。</summary>
    public IEnumerable<PluginSettingDefinition> Settings => Definition.SettingSections.SelectMany(section => section.Settings);

    public string GetDisplayName(CultureInfo culture)
    {
        return PluginTexts.Resolve(Definition.Names, culture)
            ?? throw new InvalidOperationException($"The validated widget '{Definition.Id}' has no name.");
    }

    internal void MarkStopped()
    {
        IsStopped = true;
    }

    /// <summary>
    /// 窓の中身を作る(窓を開くたび、画面のスレッドで)。プラグインの例外と、返した中身が定義に合わないときは、
    /// 受け止め(<see cref="PluginCallGuard"/>)がそのプラグインを止め、null を返す。
    /// </summary>
    /// <param name="targetId">相手ごとに窓を開くウィジェットの相手の ID。窓が1枚だけのウィジェットは null。</param>
    internal PluginWidgetWindowViewModel? CreateWindowViewModel(WidgetListItemViewModel widget, string? targetId)
    {
        var viewModel = CreateEmptyWindowViewModel();
        return TrySetWindowContent(viewModel, widget, targetId) ? viewModel : null;
    }

    /// <summary>中身の無い窓の値を作る(相手ごとに窓を開くウィジェットの空の窓)。プラグインは呼ばない。</summary>
    internal PluginWidgetWindowViewModel CreateEmptyWindowViewModel()
    {
        return new PluginWidgetWindowViewModel(Definition.ScrollsVertically, _guard);
    }

    /// <summary>
    /// プラグインに中身を作らせて窓の値へ入れる。プラグインの例外と、返した中身が定義に合わないときは、
    /// 受け止めがそのプラグインを止め、false を返す。
    /// </summary>
    internal bool TrySetWindowContent(PluginWidgetWindowViewModel viewModel, WidgetListItemViewModel widget, string? targetId)
    {
        var context = new PluginWidgetContext(Definition.Id, this, widget, _guard);
        if (!_guard.TryInvoke(
                "Widget content creation",
                () => Validate(_multiProvider is not null
                    ? _multiProvider.CreateWidget(Definition.Id, targetId!, context)
                    : _provider!.CreateWidget(Definition.Id, context)),
                out var content))
        {
            context.Detach();
            return false;
        }

        viewModel.SetContent(content, context);
        return true;
    }

    /// <summary>
    /// 相手の一覧を読んで確かめた写しを返す(画面のスレッドで)。プラグインの例外と決まりに合わない一覧は、
    /// 受け止めがそのプラグインを止め、空を返す。空の一覧は決まりに合っている(今は相手がいないだけ)。止めた後は呼ばない。
    /// </summary>
    internal IReadOnlyList<PluginWidgetTarget> ReadTargets()
    {
        if (IsStopped || _multiProvider is null)
        {
            return [];
        }

        return _guard.TryInvoke<IReadOnlyList<PluginWidgetTarget>>(
            "Widget targets",
            () => PluginWidgetValidator.TryCopyTargets(_multiProvider.GetWidgetTargets(Definition.Id), out var copies, out var error)
                ? copies
                : throw new InvalidOperationException($"The targets of the widget '{Definition.Id}' are invalid: {error}"),
            out var targets)
            ? targets
            : [];
    }

    private PluginWidgetContent Validate(PluginWidgetContent? content)
    {
        if (content?.Body is null)
        {
            throw new InvalidOperationException($"CreateWidget returned no body for the widget '{Definition.Id}'.");
        }

        if (Definition.HasFooter != content.Footer is not null)
        {
            throw new InvalidOperationException(Definition.HasFooter
                ? $"The widget '{Definition.Id}' has a footer row, but CreateWidget returned no footer."
                : $"The widget '{Definition.Id}' has no footer row, but CreateWidget returned a footer.");
        }

        if (content.HeaderButtons is null || content.HeaderButtons.Any(button => button is null))
        {
            throw new InvalidOperationException($"CreateWidget returned a null header button list or button for the widget '{Definition.Id}'.");
        }

        return content;
    }
}

/// <summary>動いているプラグイン1つのウィジェット。</summary>
public sealed class PluginWidgetGroup
{
    internal PluginWidgetGroup(PluginInfo info, IReadOnlyList<PluginWidgetRegistration> widgets)
    {
        Info = info;
        Widgets = widgets;
    }

    public PluginInfo Info { get; }

    public string PluginKey => Info.Key;

    public IReadOnlyList<PluginWidgetRegistration> Widgets { get; }
}

/// <summary>
/// 動いているプラグインのウィジェット(全部のプラグインで1つ、画面のスレッドで使う)。
/// メイン画面はこの知らせでカードを足し外しする。止めたプラグインのカードは外し、窓は閉じる(保存した設定と状態は残す)。
/// </summary>
internal sealed class PluginWidgetRegistry
{
    private readonly List<PluginWidgetGroup> _groups = [];

    private PluginWidgetRegistry()
    {
    }

    public static PluginWidgetRegistry Instance { get; } = new();

    /// <summary>ウィジェットのあるプラグイン。並びは足した順(プラグインの一覧と同じ)。</summary>
    public IReadOnlyList<PluginWidgetGroup> Groups => _groups;

    public event EventHandler<PluginWidgetGroup>? GroupAdded;

    public event EventHandler<PluginWidgetGroup>? GroupRemoved;

    /// <summary>
    /// 初期化できたプラグインの、検証した定義を受け取る(窓が1枚だけのウィジェット、相手ごとに窓を開くウィジェットの順にカードを並べる)。
    /// ウィジェットが1つも無ければ何も足さない。
    /// </summary>
    public void Register(
        PluginInfo info,
        IReadOnlyList<PluginWidgetDefinition> definitions,
        IReadOnlyList<PluginWidgetDefinition> multiWindowDefinitions,
        PluginCallGuard guard,
        IPluginWidgetProvider? provider,
        IPluginMultiWidgetProvider? multiProvider)
    {
        if (definitions.Count == 0 && multiWindowDefinitions.Count == 0)
        {
            return;
        }

        var group = new PluginWidgetGroup(
            info,
            [
                .. definitions.Select(definition => new PluginWidgetRegistration(info, definition, guard, provider, null)),
                .. multiWindowDefinitions.Select(definition => new PluginWidgetRegistration(info, definition, guard, null, multiProvider))
            ]);
        _groups.Add(group);
        GroupAdded?.Invoke(this, group);
    }

    /// <summary>プラグインを止めた。そのウィジェットのカードを外させる。</summary>
    public void Unregister(string pluginKey)
    {
        var index = _groups.FindIndex(group => string.Equals(group.PluginKey, pluginKey, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        var group = _groups[index];
        _groups.RemoveAt(index);
        foreach (var widget in group.Widgets)
        {
            widget.MarkStopped();
        }

        GroupRemoved?.Invoke(this, group);
    }
}
