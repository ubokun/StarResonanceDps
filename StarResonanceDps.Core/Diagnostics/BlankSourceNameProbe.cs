using System.Globalization;
using System.Text.RegularExpressions;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.Core.Diagnostics;

/// <summary>
/// スキル詳細ウィジェットの行(プレイヤーの与ダメ・ヒール)に<b>名前が入っていない</b>鍵を残す常設の計測。
///
/// <para>
/// <b>呼ぶのは記録の直後</b>(<c>Encounter.ResolveSourceLanding</c>)。ウィジェットを開いていなくても、
/// 全プレイヤーの与ダメ・ヒールの両方で拾う。行を作る側に置くと、開いている1人・1種別しか見えない。
/// </para>
///
/// <para>
/// メーターの行名は <c>Data/Localization/RecountRows.json</c>(ゲーム内メーターの見出し表の写し)が先に決める。
/// 総括行(其他)は行ごと落としてあるので、そこに居た鍵と見出し表にそもそも無い鍵は、
/// 記録時に付与元をたどった着地先(オプションか技、<c>Services.SourceLandingResolver</c>)の名前で出る。
/// ここに残すのは、どこにも着かず<b>名前が空のまま内部ID注記だけ</b>で出る鍵と、技に着いた鍵(オプションより確かさが一段低い)、
/// 着地先が食い違った鍵。どれも<b>たどった鎖</b>を書く。
/// 着かなかった鍵は、後の記録で着けば DB に空欄が残らないので、記録の時点では書かずにエンカウンターに控え、
/// DB へ保存した直後にまだ着いていないものだけを書く(<c>Encounter.ReportUnresolvedBlankSources</c>)。
/// </para>
///
/// <para>
/// 出た鍵は <c>Data/Overrides/RecountRowOverrides.json</c> に手当てする候補になる。
/// 行に寄せるなら <c>Row</c>、独立した行として名前を付けるなら <c>Name</c>。
/// <b>値は実物を確認してから入れること。</b> 根拠のない名前は、空欄のままより悪い。
/// </para>
///
/// <para>
/// <b>判定は注記を付ける前の生名(<c>GetSourceName</c>)で行う。</b>
/// 表示名は空欄でも <c>(2203531:1)</c> の注記が付くので空文字にならず、
/// しかも注記は表示設定で消えるため、表示名で見ると設定次第で検知が変わる。
/// </para>
///
/// <para>
/// <b>同じ鍵は1起動に1回だけ送る。</b> 同じ鍵はヒットのたびに届き、一覧も種類と番号ごとに1件しか持たない。
/// </para>
///
/// <para>
/// <b>これは調査用の一時計測ではない。</b> 常設で、プローブ撤去の対象外。
/// 検知はフィードバックタブの一覧(<see cref="ResidentDetectionStore"/>)へ送り、報告の本文になる。
/// 本文は公開されるので、鎖の中の実体の番号(プレイヤーの uuid が入ることがある)は外す。
/// どこで出たかの手がかりに、検知した戦闘の記録のマップの番号を <c>scene=</c> で添える(分からなければ 0)。
/// </para>
/// </summary>
public static partial class BlankSourceNameProbe
{
    private static readonly object Sync = new();
    private static readonly HashSet<long> SeenRowKeys = [];
    private static readonly HashSet<long> SeenConflicts = [];

    /// <summary>
    /// 見出し表で名前が空の行を1件送る。保存の時点で着いていなかったもの(<paramref name="landing"/> が無し)と、技に着いたものを送る。
    /// </summary>
    /// <param name="rowKey">畳んだあとの行代表キー。<c>ownerId:枝番</c> で書き出す。</param>
    /// <param name="rowKeyText">その鍵の表示形。手修正ファイルへそのまま写せる形で残す。</param>
    /// <param name="isBuffSource">届いたときの種別。名前には効かないが、正体を追う手掛かりになる。</param>
    /// <param name="isHealing">与ダメとヒールのどちらの記録で出たか。</param>
    /// <param name="landing">付与元をたどって着いた先。着かなければ無し。</param>
    /// <param name="sceneId">検知した戦闘の記録のマップの番号。分からなければ 0。</param>
    /// <param name="trace">たどった鎖(段ごとの種類とID、止まった理由)。</param>
    public static void Capture(
        long rowKey,
        string rowKeyText,
        bool isBuffSource,
        bool isHealing,
        SourceLanding landing,
        uint sceneId,
        string trace)
    {
        lock (Sync)
        {
            if (!SeenRowKeys.Add(rowKey))
            {
                return;
            }
        }

        var details = string.Create(
            CultureInfo.InvariantCulture,
            $"type={(isBuffSource ? "buff" : "skill")} table={(isHealing ? "heal" : "damage")} landing={landing} scene={sceneId}");
        ResidentDetectionStore.Add(ResidentDetectionKind.BlankSourceName, rowKeyText, $"{details} chain={RemoveEntityIds(trace)}");
    }

    /// <summary>
    /// 着地先が決まった鍵で、別の先に着いた回を送る。同じ鍵は1起動に1回。<paramref name="sceneId"/> は <see cref="Capture"/> と同じ。
    /// </summary>
    public static void CaptureConflict(
        long rowKey,
        string rowKeyText,
        bool isHealing,
        SourceLanding decided,
        SourceLanding other,
        uint sceneId,
        string trace)
    {
        lock (Sync)
        {
            if (!SeenConflicts.Add(rowKey))
            {
                return;
            }
        }

        var details = string.Create(
            CultureInfo.InvariantCulture,
            $"table={(isHealing ? "heal" : "damage")} decided={decided} other={other} scene={sceneId}");
        ResidentDetectionStore.Add(ResidentDetectionKind.SourceLandingConflict, rowKeyText, $"{details} chain={RemoveEntityIds(trace)}");
    }

    /// <summary>鎖の中の実体の番号を外す。プレイヤーの uuid が入ることがあり、報告の本文は公開される。</summary>
    private static string RemoveEntityIds(string trace)
    {
        return EntityIdRegex().Replace(trace, "$1");
    }

    [GeneratedRegex(@"(caster|entity) -?\d+")]
    private static partial Regex EntityIdRegex();
}
