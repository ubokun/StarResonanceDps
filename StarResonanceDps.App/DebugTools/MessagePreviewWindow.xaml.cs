// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Serilog;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.Views;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.DebugTools;

/// <summary>
/// アプリのメッセージウィンドウ・確認ウィンドウを、エラーを起こさずに出して見た目を確かめる。Debug の構成でだけビルドされる。
/// 本物の呼び出しと同じ文言の鍵の組を並べてある。本物の窓を足したり本文と詳細の分け方を変えたら、ここも直す。
/// モジュールツールの窓は、プラグインの文言(<see cref="PluginLocalizer"/>)を引き、プラグインの文が通る本体側の表示(<see cref="PluginMessageService"/>)で出す。
/// </summary>
public partial class MessagePreviewWindow : Window
{
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    // 「設定できませんでした(エラー N)」の文を出すための試しの値。
    private const int SampleOtherError = 5;

    // 更新の進み具合の窓を出すための試しの値。
    private const double SampleUpdateRatio = 0.4;
    private const double SampleUpdateDoneMegabytes = 21.8;
    private const double SampleUpdateTotalMegabytes = 54.6;

    // プラグインの更新の窓を出すための試しの値。
    private const string SamplePluginVersion = "2.0.1";

    // 検出された項目の報告の窓を出すための試しの値。
    private const int SampleOpenedPageCount = 3;

    private const string ModuleAssemblyName = "ModuleTool";
    private const string ModuleResourceBaseName = "StarResonanceDps.Plugins.ModuleTool.Properties.Resources";
    private const string ModuleConfigFileName = "module_config.json";

    private readonly PluginMessageService _pluginMessages = new();
    private readonly PluginLocalizationService _pluginLocalization = new(new PluginCallGuard((operation, exception) =>
        Log.Error(exception, "[MessagePreview] {Operation} failed", operation)));
    private PluginLocalizer? _moduleTexts;

    public MessagePreviewWindow()
    {
        InitializeComponent();
        Closed += (_, _) =>
        {
            _moduleTexts?.Dispose();
            _pluginLocalization.Detach();
        };
    }

    private static LocalizationManager Localization => LocalizationManager.Instance;

    private PluginLocalizer ModuleTexts => _moduleTexts ??= new PluginLocalizer(
        _pluginLocalization,
        LoadPluginAssembly(ModuleAssemblyName),
        ModuleResourceBaseName);

    // モジュールツールが使う設定ファイルの場所(プラグインの設定は本体がプラグインのフォルダに置く)。
    private static string ModuleConfigFilePath => Path.Combine(AppDataPaths.PluginsDirectory, ModuleAssemblyName, ModuleConfigFileName);

    // 本物: 起動時(実行フォルダの Data に書けないとき)。場所は実際の Data のフォルダ。
    private void DataFolderNotWritable_Click(object sender, RoutedEventArgs e)
    {
        DataFolderNotWritableMessage.Show(this, CombatRuntimePaths.DataDirectory);
    }

    // 本物: 起動時のメイン窓(設定ファイルを読めなかったとき)。場所は実際の AppSettings.json。
    private void SettingsLoadFailed_Click(object sender, RoutedEventArgs e)
    {
        SettingsLoadFailureMessage.Show(this, AppDataPaths.AppSettingsPath);
    }

    // 本物: 起動時のメイン窓・全体設定の保存(Windows起動時の起動を書けなかったとき)
    private void WindowsStartupRegistrationFailed_Click(object sender, RoutedEventArgs e)
    {
        WindowsStartupRegistrationFailureMessage.Show(this);
    }

    // 本物: 起動時のメイン窓(起動設定で VOICEVOX を起動できなかったとき)
    private void VoicevoxLaunchFailed_Click(object sender, RoutedEventArgs e)
    {
        VoicevoxLaunchFailureMessage.Show(this);
    }

    // 本物: 新しいバージョンがあるとき(起動時・ヘルプタブの「更新を確認」)。バージョンは今のもの。
    private void UpdateConfirm_Click(object sender, RoutedEventArgs e)
    {
        _ = UpdateConfirmMessage.Show(this, AppUpdateService.CurrentVersion);
    }

    // 本物: 更新の取得と置き換えの進み具合。キャンセルで閉じる。
    private void UpdateProgress_Click(object sender, RoutedEventArgs e)
    {
        var window = UpdateProgressWindow.Create(this);
        window.CancelRequested += (_, _) => window.CloseNow();
        window.ShowStatus("Update_Progress_Downloading");
        window.ShowProgress(SampleUpdateRatio, Localization.Format("Update_Progress_Size", SampleUpdateDoneMegabytes, SampleUpdateTotalMegabytes));
        OwnerModalWindow.Show(window, this);
    }

    // 本物: 更新の置き換えが済んだ後の起動
    private void UpdateCompleted_Click(object sender, RoutedEventArgs e)
    {
        UpdateCompletedMessage.Show(this);
    }

    // 本物: ヘルプタブの「更新を確認」(確認できなかったとき)
    private void UpdateCheckError_Click(object sender, RoutedEventArgs e)
    {
        UpdateFailureMessage.ShowCheckFailed(this, AppUpdateFailure.Network);
    }

    // 本物: 更新の取得と置き換え(失敗したとき)。理由はボタンの Tag。
    private void UpdateError_Click(object sender, RoutedEventArgs e)
    {
        UpdateFailureMessage.Show(this, (AppUpdateFailure)((FrameworkElement)sender).Tag);
    }

    // 本物: 更新のモード(置き換えは済んだが起動し直せなかったとき)
    private void UpdateRestartFailed_Click(object sender, RoutedEventArgs e)
    {
        UpdateFailureMessage.ShowRestartFailed(this, isUpdated: true);
    }

    // 本物: 更新のモード(置き換えなかった元のバージョンを起動し直せなかったとき)
    private void UpdateRestartFailedNotUpdated_Click(object sender, RoutedEventArgs e)
    {
        UpdateFailureMessage.ShowRestartFailed(this, isUpdated: false);
    }

    // 本物: フィードバックタブの「選んだ項目を報告」で、投稿画面を1つ開いたとき
    private void FeedbackOpened_Click(object sender, RoutedEventArgs e)
    {
        DetectionReportMessage.ShowOpened(this, 1);
    }

    // 本物: フィードバックタブの「選んだ項目を報告」で、1つの画面に入りきらず投稿画面を分けて開いたとき
    private void FeedbackOpenedMany_Click(object sender, RoutedEventArgs e)
    {
        DetectionReportMessage.ShowOpened(this, SampleOpenedPageCount);
    }

    // 本物: フィードバックタブの「選んだ項目を報告」で、選んだ項目が全部Issueにあったとき
    private void FeedbackAlreadyReported_Click(object sender, RoutedEventArgs e)
    {
        DetectionReportMessage.ShowAlreadyReported(this);
    }

    // 本物: フィードバックタブの「選んだ項目を報告」で、Issueを確認できなかったとき
    private void FeedbackCheckError_Click(object sender, RoutedEventArgs e)
    {
        DetectionReportMessage.ShowCheckFailed(this, (FeedbackIssueLookupFailure)((FrameworkElement)sender).Tag);
    }

    // 本物: メイン画面のプラグインの一覧の「＋」(選んだファイルがプラグインでない・版が合わないとき)
    private void PluginAddFailedInvalidFile_Click(object sender, RoutedEventArgs e)
    {
        PluginAddFailureMessage.ShowInvalidFile(this);
    }

    // 本物: メイン画面のプラグインの一覧の「＋」(Pluginsフォルダへ写せないとき)
    private void PluginAddFailedCannotPlace_Click(object sender, RoutedEventArgs e)
    {
        PluginAddFailureMessage.ShowCannotPlaceFile(this);
    }

    // 本物: メイン画面のプラグインの一覧の「＋」(同じプラグインの、同じか古いバージョンを選んだとき)
    private void PluginAlreadyAdded_Click(object sender, RoutedEventArgs e)
    {
        PluginAlreadyAddedMessage.Show(this);
    }

    // 本物: メイン画面のプラグインの一覧の「＋」(プラグインが使うファイルが選んだDLLのフォルダに無いとき)。ファイル名は同梱のプラグインとサンプルのもの。
    private void PluginAddFailedMissingDependency_Click(object sender, RoutedEventArgs e)
    {
        PluginAddFailureMessage.ShowMissingFiles(this, [ModuleAssemblyName + ".dll", "SampleDependency.Library.dll"]);
    }

    // 本物: メイン画面のプラグインの一覧の「＋」(同じプラグインがアンインストールして削除を待っているとき)
    private void PluginAddFailedPendingDeletion_Click(object sender, RoutedEventArgs e)
    {
        PluginAddFailureMessage.ShowPendingDeletion(this);
    }

    // 本物: メイン画面のプラグインの一覧の「＋」(同じプラグインの新しいバージョンを選んだとき)。名前は今の一覧の最初のプラグイン。
    private void PluginUpdate_Click(object sender, RoutedEventArgs e)
    {
        PluginUpdateMessage.ShowPending(this, PluginNames[0], SamplePluginVersion);
    }

    // 本物: メイン画面のプラグインの一覧の「＋」(新しいバージョンのファイルを写せなかったとき)。フォルダは今の一覧の最初のプラグイン。
    private void PluginUpdateFailed_Click(object sender, RoutedEventArgs e)
    {
        PluginUpdateMessage.ShowFailed(this, PluginManager.Instance.DiscoverPlugins()[0].Key);
    }

    // 本物: メイン画面のプラグインの一覧のゴミ箱。名前は今の一覧の最初のプラグイン。
    private void ConfirmUninstallPlugin_Click(object sender, RoutedEventArgs e)
    {
        _ = PluginUninstallMessage.Confirm(this, PluginNames[0]);
    }

    // 本物: メイン画面のプラグインの一覧のゴミ箱(削除の印を書けなかったとき)。フォルダは今の一覧の最初のプラグイン。
    private void PluginDeleteFailed_Click(object sender, RoutedEventArgs e)
    {
        PluginDeleteFailureMessage.Show(this, PluginManager.Instance.DiscoverPlugins()[0].Key);
    }

    // 本物: 起動時のメイン窓と、メイン画面のプラグインの一覧の「＋」(読めなかった・初期化できなかった・削除や更新ができなかったプラグインがあるとき)。
    // 窓に出る8つの理由を順に並べ、名前は今の一覧のプラグインを順に割り当てる。足りないファイルの名前は同梱のプラグインとサンプルのもの。
    private void PluginLoadFailed_Click(object sender, RoutedEventArgs e)
    {
        var names = PluginNames;
        PluginErrorReason[] reasons =
        [
            PluginErrorReason.MissingFiles,
            PluginErrorReason.Invalid,
            PluginErrorReason.Stopped,
            PluginErrorReason.InvalidNotifications,
            PluginErrorReason.InvalidWidgets,
            PluginErrorReason.TerminatedApp,
            PluginErrorReason.DeletionFailed,
            PluginErrorReason.UpdateFailed,
        ];
        PluginLoadFailureMessage.Show(this, reasons
            .Select((reason, index) => new PluginLoadFailure(
                names[index % names.Count],
                reason,
                reason == PluginErrorReason.MissingFiles ? [ModuleAssemblyName + ".dll", "SampleDependency.Library.dll"] : []))
            .ToArray());
    }

    // 本物: プラグインの例外で、そのプラグインを止めたとき。名前は今の一覧の最初のプラグイン。
    private void PluginStopped_Click(object sender, RoutedEventArgs e)
    {
        PluginStoppedMessage.Show(this, PluginNames[0], isPacketBacklog: false);
    }

    // 本物: プラグインが通信を取り残して、そのプラグインを止めたとき。名前は今の一覧の最初のプラグイン。
    private void PluginStoppedPacketBacklog_Click(object sender, RoutedEventArgs e)
    {
        PluginStoppedMessage.Show(this, PluginNames[0], isPacketBacklog: true);
    }

    private static IReadOnlyList<string> PluginNames => PluginManager.Instance.DiscoverPlugins()
        .Select(plugin => plugin.GetDisplayName(Localization.CurrentCulture))
        .ToArray();

    // 本物: MainWindow(ホットキーの補助が使えなかったとき)
    private void HotkeyHelperUnavailable_Click(object sender, RoutedEventArgs e)
    {
        MessageWindow.Show(
            this,
            Localization.GetString("Hotkey_Error_Title"),
            Localization.GetString("Hotkey_HelperUnavailable_Message"),
            Localization.GetString("Hotkey_HelperUnavailable_Detail"));
    }

    // 本物: 起動時・全体設定(ホットキーを設定できなかったとき)。全部の操作を既定の割り当てで並べる。
    private void HotkeyRegisterFailed_Click(object sender, RoutedEventArgs e)
    {
        var bindings = AppConfigDefaults.CreateDefaultHotkeys();
        var failures = Enum.GetValues<HotkeyAction>()
            .Select((action, index) => new HotkeyRegistrationFailure(
                action,
                bindings.Get(action),
                index % 2 == 0 ? ErrorHotkeyAlreadyRegistered : SampleOtherError))
            .ToList();
        HotkeyRegistrationFailureMessage.Show(this, failures);
    }

    // 本物: 起動時(Npcap が入っていないとき)。「はい」は本物と同じく Npcap のページを開く(終了はしない)。
    private void NpcapInstall_Click(object sender, RoutedEventArgs e)
    {
        NpcapInstallMessage.Show(this);
    }

    // 本物: 起動時(初期化で例外が出たとき)
    private void StartupInitializationFailed_Click(object sender, RoutedEventArgs e)
    {
        StartupInitializationFailedMessage.Show(this);
    }

    // 本物: 受け手の無い例外で終わるとき(終了はしない)
    private void UnhandledException_Click(object sender, RoutedEventArgs e)
    {
        UnhandledExceptionMessage.Show(this);
    }

    // 本物: 起動時のメイン窓(Npcap が古いとき)。バージョンはこの PC に入っている Npcap のもの。
    private void NpcapOutdated_Click(object sender, RoutedEventArgs e)
    {
        NpcapOutdatedMessage.Show(this, NpcapVersionProbe.GetVersion());
    }

    // 本物: 全体設定・ウィジェット設定(保存せずに閉じるとき)
    private void DiscardUnsaved_Click(object sender, RoutedEventArgs e)
    {
        _ = ConfirmWindow.Show(
            this,
            "Confirm_DiscardUnsaved_Title",
            "Confirm_DiscardUnsaved_Message",
            "Confirm_DiscardUnsaved_Detail");
    }

    // 本物: 全体設定(初期化)
    private void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        _ = ConfirmWindow.Show(
            this,
            "Confirm_ResetSettings_Title",
            "Confirm_ResetSettings_Message",
            "Confirm_ResetSettings_Detail");
    }

    // 本物: ウィジェット設定(初期化)
    private void ResetWidgetSettings_Click(object sender, RoutedEventArgs e)
    {
        _ = ConfirmWindow.Show(
            this,
            "Confirm_ResetWidgetSettings_Title",
            "Confirm_ResetWidgetSettings_Message",
            "Confirm_ResetWidgetSettings_Detail");
    }

    // 本物: プレイヤー情報(画像を保存できなかったとき)
    private void SaveImageFailed_Click(object sender, RoutedEventArgs e)
    {
        MessageWindow.Show(
            this,
            Localization.GetString("PlayerInfo_SaveImageFailed_Title"),
            Localization.GetString("PlayerInfo_SaveImageFailed_Message"),
            Localization.GetString("PlayerInfo_SaveImageFailed_Detail"));
    }

    // 本物: プレイヤー情報(画像をコピーできなかったとき)
    private void CopyImageFailed_Click(object sender, RoutedEventArgs e)
    {
        MessageWindow.Show(
            this,
            Localization.GetString("PlayerInfo_CopyImageFailed_Title"),
            Localization.GetString("PlayerInfo_CopyImageFailed_Message"),
            Localization.GetString("PlayerInfo_CopyImageFailed_Detail"));
    }

    // 本物: タイムライン・チャット(選んだ文字をコピーできなかったとき)
    private void CopyTextFailed_Click(object sender, RoutedEventArgs e)
    {
        MessageWindow.Show(
            this,
            Localization.GetString("TextCopyFailed_Title"),
            Localization.GetString("TextCopyFailed_Message"),
            Localization.GetString("TextCopyFailed_Detail"));
    }

    // 本物: 起動時・通知タブ(VOICEVOX につながらないとき)
    private void VoicevoxConnectFailed_Click(object sender, RoutedEventArgs e)
    {
        NotificationCheckMessage.ShowVoicevoxFailure(this, VoicevoxResultKind.ConnectFailed);
    }

    // 本物: 起動時・通知タブ(VOICEVOX の応答を読めないとき)
    private void VoicevoxBadResponse_Click(object sender, RoutedEventArgs e)
    {
        NotificationCheckMessage.ShowVoicevoxFailure(this, VoicevoxResultKind.BadResponse);
    }

    // 本物: 通知タブ(利用規約のリンクを押したとき)。規約の本文は起動中のエンジンから取る。
    // 話者は通知タブの項目で選んであるもの(並びの最初)、無ければエンジンの一覧の最初。つながらなければ本物と同じく失敗を知らせる。
    private async void VoicevoxPolicy_Click(object sender, RoutedEventArgs e)
    {
        var savedItem = ConfigManager.Instance.GetNotificationsSnapshot().AllItems
            .FirstOrDefault(item => item.VoicevoxSpeakerUuid.Length > 0);
        if (savedItem is not null)
        {
            await NotificationCheckMessage.ShowVoicevoxPolicyAsync(this, savedItem.VoicevoxSpeakerUuid, savedItem.VoicevoxSpeakerName);
            return;
        }

        var styles = await VoicevoxClient.GetStylesAsync();
        if (styles.Kind != VoicevoxResultKind.Success || styles.Value!.Count == 0)
        {
            NotificationCheckMessage.ShowVoicevoxFailure(this, styles.Kind);
            return;
        }

        await NotificationCheckMessage.ShowVoicevoxPolicyAsync(this, styles.Value[0].SpeakerUuid, styles.Value[0].SpeakerName);
    }

    // 本物: 起動時・通知タブ・全体設定の言語(表示言語の Windows の音声が無いとき)。言語は今の表示言語。
    private void WindowsVoiceMissing_Click(object sender, RoutedEventArgs e)
    {
        NotificationCheckMessage.ShowWindowsVoiceMissing(this);
    }

    // 本物: 集計タブ(押した履歴の回を読み込めなかったとき)
    private void HistoryLoadFailed_Click(object sender, RoutedEventArgs e)
    {
        HistoryLoadFailureMessage.Show(this);
    }

    // 本物: モジュールツール(組み合わせの検索で例外が出た)
    private void ModuleSearchError_Click(object sender, RoutedEventArgs e)
    {
        _pluginMessages.Show(
            ModuleTexts["Module.Message.SearchError.Title"],
            ModuleTexts["Module.Message.SearchError.Message"],
            ModuleTexts["Module.Message.SearchError.Detail"]);
    }

    // 本物: モジュールツール(設定を保存できなかった。起動中の最初の1回だけ出る)。場所は実際の module_config.json。
    private void ModuleSaveError_Click(object sender, RoutedEventArgs e)
    {
        _pluginMessages.Show(
            ModuleTexts["Module.Message.SaveError.Title"],
            ModuleTexts["Module.Message.SaveError.Message"],
            ModuleConfigFilePath);
    }

    // 本物: モジュールツール(開くときに設定ファイルを読めなかった。窓が出たところで出る)。場所は実際の module_config.json。
    private void ModuleSettingsLoadError_Click(object sender, RoutedEventArgs e)
    {
        _pluginMessages.Show(
            ModuleTexts["Module.Message.SettingsLoadError.Title"],
            ModuleTexts["Module.Message.SettingsLoadError.Message"],
            ModuleTexts.Format("Module.Message.SettingsLoadError.Detail", ModuleConfigFilePath));
    }

    // 本体が読んだプラグインの DLL(プラグインごとの読み込み先)から取る。
    private static Assembly LoadPluginAssembly(string assemblyName)
    {
        return PluginManager.Instance.FindPluginAssembly(assemblyName)
            ?? throw new InvalidOperationException($"The plugin '{assemblyName}' is not loaded.");
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
