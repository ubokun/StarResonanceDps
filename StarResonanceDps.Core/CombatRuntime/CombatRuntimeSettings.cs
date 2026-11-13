// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime.DataTypes;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// Core が実行時に読む設定。
///
/// <para>
/// <b>ファイルI/Oを持たない。</b> 永続化は App の <c>AppSettings.json</c> が受け持ち、
/// 起動時と設定の保存時に <see cref="Apply"/> で流し込む。Core は App を参照できないので、
/// 値は必ず外から入れてもらう。
/// </para>
///
/// <para>
/// <b>集計の設定は、パケットを処理するスレッドで、メッセージとメッセージのあいだに書き換える</b>
/// (<c>MessageManager.RunOnPacketThread</c>。キャプチャが動いていなければ呼んだスレッドでその場で書く)。
/// メッセージの処理の途中で値が変わらないようにするため。キャプチャの3項目はメッセージの処理が読まないので、呼んだスレッドで書く。
/// </para>
///
/// <para>
/// <b>ここに項目を足すときは、UIも一緒に作ること。</b> 値を変える手段が無い設定は、
/// 既定値を直書きするのと変わらない。
/// </para>
/// </summary>
public static class CombatRuntimeSettings
{
    /// <summary>ネットワークアダプターの自動選択を表す名前。</summary>
    public const string AutomaticNetCaptureDeviceName = "Auto";

    /// <summary>
    /// <see cref="Apply"/> の値が一度でも書かれたか。<c>CombatRuntimeHost.Initialize</c> の関門に使う
    /// (起動時はキャプチャが動いていないので、<see cref="Apply"/> の中で立つ)。
    /// 呼ばずに動かすと、既定値で動いているのか設定どおりなのかが区別できない。
    /// </summary>
    public static bool HasBeenApplied { get; private set; }

    /// <summary>選んだキャプチャ用アダプター。<c>Auto</c> と空白は自動選択。</summary>
    public static string NetCaptureDeviceName { get; private set; } = AutomaticNetCaptureDeviceName;

    /// <summary>ゲームの実行ファイルの特定方法。</summary>
    public static EGameCapturePreference GameCapturePreference { get; private set; }

    /// <summary><see cref="EGameCapturePreference.Custom"/> のときに使う実行ファイル名(拡張子なし)。</summary>
    public static string GameCaptureCustomExeName { get; private set; } = string.Empty;

    /// <summary>
    /// 設定「非戦闘エリアでの集計を停止」。回のシーンが非戦闘エリア(アステルリーズ・ホームガーデン・ホームハウス)なら、
    /// 戦闘の時計を立てず、回へ何も記録しないか。
    /// 時計の判定と記録の門のたびに見る(<c>Encounter.IsAggregationStopped</c>)ので、保存すれば次のメッセージから効く。
    /// </summary>
    public static bool StopAggregationInNonCombatAreas { get; private set; }

    /// <summary>
    /// 設定「進行で自動リセット」。ダンジョンの開始・ボス部屋の入場・全滅でエンカウンターを分けるか。
    /// 開始は <c>BattleStateMachine</c>、ボス部屋の入場と全滅は <c>MessageManager.ProcessAoiSyncDelta</c> が見て(全滅はそのパケットを処理し終えたところで)、<c>EnterDungeon</c> で作り直す。
    /// </summary>
    public static bool SplitEncountersOnNewPhases { get; private set; }

    /// <summary>
    /// 設定「次のイベントまで結果を保持」。新しいエンカウンターに記録(与ダメ・回復・タイムラインと被ダメログの行)が入るまで、前の結果をメーターに出し続けるか。
    /// 保持するのは「進行で自動リセット」の区切りとマップ移動で、手動のリセット・計測の開始と停止・ログアウトはすぐ新しい回を出す。
    /// 回を作るとき(<c>EncounterManager.EnterDungeon</c>)、メーターとプレイヤーリストが映す回を決めるとき(<c>MeterSnapshotProvider.ResolveActiveEncounter</c>)、
    /// 前の回を手放すとき(<c>EncounterManager.ReleaseHeldPastEncounter</c>)に見る。
    /// </summary>
    public static bool KeepPastEncounterInMeterUntilNextDamage { get; private set; }

    /// <summary>
    /// 履歴を開いている間にライブ側でイベント(戦闘・エンカウンターの作り直し)が起きたら、
    /// ライブへ戻すか。<c>EncounterHistoryProvider.NotifyLiveEncounterEvent</c> が見る。
    /// <b>計測とリセットもこの設定に従う</b>(どちらもエンカウンターの作り直しとして届く)。
    /// </summary>
    public static bool ClearHistorySelectionOnNextEvent { get; private set; } = true;


    /// <summary>
    /// 戦闘履歴を残す最大の件数。<b>0 は無限</b>で、そのときは掃除を行わない。
    /// 保存のたびとアプリ終了時に <c>DB.TrimEncountersToLimit</c> を通す。
    /// </summary>
    public static int DatabaseMaxEncounterCount { get; private set; } = 99;

    /// <summary>
    /// 計測の長さ(秒)。<c>EncounterManager.ToggleBenchmark</c> が計測を始めるときに控え、
    /// その計測の間は保存で変わっても控えた値で測る。
    /// </summary>
    public static int BenchmarkDurationSeconds { get; private set; } = 180;

    /// <summary>
    /// 計測で、自分が最初にダメージを与えた敵へのダメージだけを数えるか。
    /// <see cref="BenchmarkDurationSeconds"/> と同じく計測を始めるときに控える。
    /// </summary>
    public static bool BenchmarkFirstTargetOnly { get; private set; }

    /// <summary>
    /// 自動一時停止の秒数。最後の戦闘の出来事からこの秒数で戦闘の時計を止め、次の出来事で続きから数える。<b>0 は止めない</b>。
    /// <c>EncounterManager</c> が回を作るときに回へ写すので、保存で変えても効くのは次の回から。
    /// </summary>
    public static int CombatExitSeconds { get; private set; }

    /// <summary>
    /// App が持っている値を Core へ流し込む。起動時と、設定を保存したときに呼ぶ。
    /// 値の範囲の検査とキャプチャの3項目は呼んだスレッドで行い、集計の設定は処理のスレッドで書く(クラスの説明)。
    /// </summary>
    public static void Apply(
        string? netCaptureDeviceName,
        EGameCapturePreference gameCapturePreference,
        string? gameCaptureCustomExeName,
        bool stopAggregationInNonCombatAreas,
        bool splitEncountersOnNewPhases,
        bool keepPastEncounterInMeterUntilNextDamage,
        bool clearHistorySelectionOnNextEvent,
        int databaseMaxEncounterCount,
        int benchmarkDurationSeconds,
        bool benchmarkFirstTargetOnly,
        int combatExitSeconds)
    {
        if (benchmarkDurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(benchmarkDurationSeconds), benchmarkDurationSeconds, "Benchmark duration must be positive.");
        }

        if (combatExitSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(combatExitSeconds), combatExitSeconds, "Combat exit seconds must not be negative.");
        }

        ApplyCaptureSettings(netCaptureDeviceName, gameCapturePreference, gameCaptureCustomExeName);

        // 集計の設定はメッセージの処理が読むので、メッセージとメッセージのあいだでだけ書き換える。
        // 1つのメッセージの中で読み直しても同じ値になり、時計の判定と記録の門が食い違わない。
        MessageManager.RunOnPacketThread(() =>
        {
            var wasApplied = HasBeenApplied;
            StopAggregationInNonCombatAreas = stopAggregationInNonCombatAreas;
            SplitEncountersOnNewPhases = splitEncountersOnNewPhases;
            KeepPastEncounterInMeterUntilNextDamage = keepPastEncounterInMeterUntilNextDamage;
            ClearHistorySelectionOnNextEvent = clearHistorySelectionOnNextEvent;
            DatabaseMaxEncounterCount = databaseMaxEncounterCount;
            BenchmarkDurationSeconds = benchmarkDurationSeconds;
            BenchmarkFirstTargetOnly = benchmarkFirstTargetOnly;
            CombatExitSeconds = combatExitSeconds;
            HasBeenApplied = true;

            // 保持をオフにしたら、保持していた前の回をその場で手放し、プレイヤーリストの名簿もメーターと同じ回へ作り直す。
            // 起動時の最初の適用ではまだ回が無いので触らない(EncounterManager を起動の関門より前に動かさない)。
            if (wasApplied && !keepPastEncounterInMeterUntilNextDamage)
            {
                EncounterManager.ReleaseHeldPastEncounter();
            }
        });
    }

    /// <summary>
    /// キャプチャの3項目だけを差し替える。アダプターを選び直したときに
    /// <c>NetworkAdapterSession</c> から呼ぶ。
    ///
    /// <para>
    /// <b>ここでは保存しない。</b> ファイルへ書くのは App の仕事で、
    /// <c>NetworkAdapterSession.CaptureSettingsPersistRequested</c> がその合図になる。
    /// </para>
    /// </summary>
    public static void ApplyCaptureSettings(
        string? netCaptureDeviceName,
        EGameCapturePreference gameCapturePreference,
        string? gameCaptureCustomExeName)
    {
        NetCaptureDeviceName = IsAutomaticNetCaptureDeviceName(netCaptureDeviceName)
            ? AutomaticNetCaptureDeviceName
            : netCaptureDeviceName!.Trim();
        GameCapturePreference = gameCapturePreference;
        GameCaptureCustomExeName = Path.GetFileNameWithoutExtension(gameCaptureCustomExeName ?? string.Empty);

        MessageManager.NetCaptureDeviceName = IsAutomaticNetCaptureDeviceName(NetCaptureDeviceName)
            ? string.Empty
            : NetCaptureDeviceName;
        MessageManager.GameCapturePreference = GameCapturePreference;
        MessageManager.GameCaptureCustomExeName = GameCaptureCustomExeName;
    }

    /// <summary>空・空白・<c>Auto</c> はどれも自動選択として扱う。</summary>
    public static bool IsAutomaticNetCaptureDeviceName(string? deviceName)
    {
        return string.IsNullOrWhiteSpace(deviceName)
            || string.Equals(deviceName.Trim(), AutomaticNetCaptureDeviceName, StringComparison.OrdinalIgnoreCase);
    }
}
