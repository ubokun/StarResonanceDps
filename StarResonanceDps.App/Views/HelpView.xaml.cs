// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views;

/// <summary>ヘルプタブ。アプリの名前・バージョン・更新の確認・紹介と、使い方などのページへのリンク。</summary>
public partial class HelpView : UserControl
{
    private readonly HelpViewModel _viewModel = new();

    private bool _isSubscribed;

    internal ScrollViewer ContentScrollViewer => HelpScrollViewer;

    public HelpView()
    {
        InitializeComponent();
        DataContext = _viewModel;

        Loaded += HelpView_Loaded;
        Unloaded += HelpView_Unloaded;
    }

    private void HelpView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isSubscribed)
        {
            return;
        }

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        AppUpdateService.StatusChanged += AppUpdateService_StatusChanged;
        _isSubscribed = true;

        // 外れていた間に言語や更新の確認の状態が替わっていても、ここで入れ直す。
        _viewModel.RefreshTexts();
    }

    private void HelpView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!_isSubscribed)
        {
            return;
        }

        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        AppUpdateService.StatusChanged -= AppUpdateService_StatusChanged;
        _isSubscribed = false;
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        _viewModel.RefreshTexts();
    }

    private void AppUpdateService_StatusChanged(object? sender, EventArgs e)
    {
        _viewModel.RefreshTexts();
    }
}
