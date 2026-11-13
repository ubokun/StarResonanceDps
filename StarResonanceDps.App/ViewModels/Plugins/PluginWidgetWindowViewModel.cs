// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.ViewModels.Plugins;

/// <summary>
/// プラグインのウィジェットの窓1つの中身。プラグインが作った本体・フッター・ヘッダーのボタンを持ち、窓を閉じたら後始末する
/// (窓が中身の <c>DataContext</c> の <see cref="IDisposable"/> を呼ぶ)。
/// 空の窓は中身なしで作り、相手が決まったら <see cref="SetContent"/> で入れる。
/// 状態の印はいつも緑(本体の理由はゲームの通信の決まりで、プラグインの中身には当てられない)。
/// </summary>
public sealed class PluginWidgetWindowViewModel : IWidgetDataStatusSource, IDisposable
{
    private readonly PluginCallGuard _guard;
    private PluginWidgetContext? _context;
    private bool _isDisposed;

    internal PluginWidgetWindowViewModel(bool scrollsVertically, PluginCallGuard guard)
    {
        ScrollsVertically = scrollsVertically;
        _guard = guard;
    }

    internal void SetContent(PluginWidgetContent content, PluginWidgetContext context)
    {
        Body = content.Body;
        Footer = content.Footer;
        HeaderButtons = [.. content.HeaderButtons.Select(button => new PluginWidgetHeaderButtonViewModel(button, _guard))];
        _context = context;
    }

    public event EventHandler? DataStatusChanged
    {
        add
        {
        }

        remove
        {
        }
    }

    /// <summary>本体。空の窓は相手が決まるまで null。</summary>
    public FrameworkElement? Body { get; private set; }

    public FrameworkElement? Footer { get; private set; }

    public bool ScrollsVertically { get; }

    public IReadOnlyList<PluginWidgetHeaderButtonViewModel> HeaderButtons { get; private set; } = [];

    public WidgetDataStatus DataStatus => WidgetDataStatus.Complete;

    public void RefreshDataStatus()
    {
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _context?.Detach();
        if (Body is not null)
        {
            DisposeElement(Body);
        }

        if (Footer is not null)
        {
            DisposeElement(Footer);
        }
    }

    private void DisposeElement(FrameworkElement element)
    {
        if (element is IDisposable disposable)
        {
            _guard.TryInvoke("Widget content disposal", disposable.Dispose);
        }
    }
}

/// <summary>ヘッダーのボタン1つ。文字はプラグインのボタンの値をそのまま出し、押したら受け止めの中で知らせる。</summary>
public sealed class PluginWidgetHeaderButtonViewModel
{
    internal PluginWidgetHeaderButtonViewModel(PluginWidgetHeaderButton button, PluginCallGuard guard)
    {
        Button = button;
        ClickCommand = new RelayCommand(() => guard.TryInvoke("Widget header button", button.RaiseClick));
    }

    public PluginWidgetHeaderButton Button { get; }

    public ICommand ClickCommand { get; }
}
