using System.Windows;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

/// <summary>更新の確認や置き換えができなかったことを出す。詳細は理由か直し方で、zipの取得が直し方なら詳細のリリースノートの語をリンクにする。</summary>
public static class UpdateFailureMessage
{
    // 書式の中のリンクを差し込む位置の印。整形した文字列をここで区切る。
    private const string ReleaseNotesLinkMarker = "\x01";

    public static void ShowCheckFailed(Window? owner, AppUpdateFailure failure)
    {
        ShowFailure(owner, "Update_CheckError_Message", failure);
    }

    public static void Show(Window? owner, AppUpdateFailure failure)
    {
        ShowFailure(owner, "Update_Error_Message", failure);
    }

    /// <summary>更新のモードの最後に、置き換えた新しいバージョンか、置き換えなかった元のバージョンを起動できなかった。</summary>
    public static void ShowRestartFailed(Window? owner, bool isUpdated)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("Update_Error_Title"),
            localization.GetString(isUpdated ? "Update_RestartFailed_Message" : "Update_RestartFailedNotUpdated_Message"),
            localization.GetString("Update_RestartFailed_Detail"));
    }

    private static void ShowFailure(Window? owner, string messageResourceKey, AppUpdateFailure failure)
    {
        var localization = LocalizationManager.Instance;
        var title = localization.GetString("Update_Error_Title");
        var message = localization.GetString(messageResourceKey);
        var detailResourceKey = GetDetailResourceKey(failure);

        if (failure is AppUpdateFailure.InvalidRelease or AppUpdateFailure.NotFromZip or AppUpdateFailure.RestoreFailed)
        {
            var detailParts = localization.Format(detailResourceKey, ReleaseNotesLinkMarker).Split(ReleaseNotesLinkMarker);
            MessageWindow.Show(
                owner,
                title,
                message,
                [
                    new MessageDetailPart(detailParts[0]),
                    new MessageDetailPart(localization.GetString("Update_ReleaseNotesLink"), ProjectLinks.ReleasesUrl),
                    new MessageDetailPart(detailParts[1])
                ]);
            return;
        }

        MessageWindow.Show(owner, title, message, localization.GetString(detailResourceKey));
    }

    private static string GetDetailResourceKey(AppUpdateFailure failure)
    {
        return failure switch
        {
            AppUpdateFailure.Network => "GitHub_Error_Network",
            AppUpdateFailure.Server => "GitHub_Error_Server",
            AppUpdateFailure.RateLimit => "GitHub_Error_RateLimit",
            AppUpdateFailure.InvalidRelease => "Update_Error_InvalidRelease",
            AppUpdateFailure.NotFromZip => "Update_Error_NotFromZip",
            AppUpdateFailure.Corrupt => "Update_Error_Corrupt",
            AppUpdateFailure.Write => "Update_Error_Write",
            AppUpdateFailure.Start => "Update_Error_Start",
            AppUpdateFailure.Replace => "Update_Error_Replace",
            AppUpdateFailure.RestoreFailed => "Update_Error_RestoreFailed",
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
        };
    }
}
