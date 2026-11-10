using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.ViewModels;

/// <summary>ヘルプタブ。アプリのバージョンと更新の確認の結果、更新の確認、プロジェクトのページ(<see cref="ProjectLinks"/>)を開く命令。</summary>
public sealed partial class HelpViewModel : ObservableObject
{
    /// <summary>
    /// 上の欄のバージョンの行(「バージョン 4.0.0(最新)」の形)。
    /// 言語を切り替えたときと更新の確認の状態が変わったときに<see cref="RefreshTexts"/>で入れ直す。
    /// </summary>
    [ObservableProperty]
    private string _versionText = string.Empty;

    /// <summary>更新のボタンの文字。確認の途中は「キャンセル」。</summary>
    [ObservableProperty]
    private string _checkForUpdatesText = string.Empty;

    public HelpViewModel()
    {
        RefreshTexts();
    }

    public void RefreshTexts()
    {
        var localization = LocalizationManager.Instance;
        var version = localization.Format("Help_Version", AppUpdateService.CurrentVersion.ToString(3));
        var statusKey = AppUpdateService.Status switch
        {
            AppUpdateStatus.Checking => "Update_Status_Checking",
            AppUpdateStatus.UpToDate => "Update_Status_UpToDate",
            AppUpdateStatus.Available => "Update_Status_Available",
            AppUpdateStatus.NoRelease => "Update_Status_NoRelease",
            AppUpdateStatus.Failed => "Update_Status_Failed",
            _ => null
        };

        VersionText = statusKey is null
            ? version
            : localization.Format("Help_VersionWithStatus", version, localization.GetString(statusKey));
        CheckForUpdatesText = localization.GetString(AppUpdateService.IsChecking ? "Common_Cancel" : "Help_CheckForUpdates");
    }

    /// <summary>確認の途中はキャンセルになるので、途中でも押せる。</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task CheckForUpdatesAsync()
    {
        if (AppUpdateService.IsChecking)
        {
            AppUpdateService.CancelCheck();
            return;
        }

        if (Application.Current?.MainWindow is { } owner)
        {
            await AppUpdateService.CheckManuallyAsync(owner);
        }
    }

    [RelayCommand]
    private void OpenLink(string url)
    {
        ExternalLinkOpener.Open(url);
    }

    /// <summary>アプリの言語で開く先が変わるページを、押した時点の言語で開く。</summary>
    [RelayCommand]
    private void OpenLocalizedLink(LocalizedProjectPage page)
    {
        ExternalLinkOpener.Open(ProjectLinks.GetLocalizedUrl(page, LocalizationManager.Instance.CurrentCulture));
    }
}
