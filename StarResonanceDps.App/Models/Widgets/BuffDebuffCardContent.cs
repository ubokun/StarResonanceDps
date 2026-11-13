// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Media;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// バフ・デバフカードが1件のバフから作る表示値。
///
/// <para>
/// プレイヤー用とモンスター用でViewModelが分かれるので、<b>組み立てはここに集める</b>。
/// 分けて書くと片方だけ直して見た目がずれる。
/// </para>
/// </summary>
/// <param name="IconMask">絵が Icons.xaml の絵(装備効果)なら、白く塗る面の型抜き(<see cref="PlayerBuffEntry.IconMask"/>)。</param>
public readonly record struct BuffDebuffCardContent(
    string DisplayText,
    string DurationText,
    string LayerText,
    string? IconPath,
    ImageBrush? IconMask,
    bool IsModuleEffectIcon,
    bool IsFactorIcon,
    bool ShowsUnknownIcon)
{
    public static BuffDebuffCardContent Empty { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        null,
        null,
        false,
        false,
        false);

    /// <summary>
    /// 生きているバフの表示値。付与者プレイヤー名(<c>{CasterName}</c>)は、出している実体の付与者がプレイヤーなら書式に入れればいつも出す
    /// (カードに付与者の表示の設定は無い)。カードはバフの番号で追うので、付与者は出す実体(残り時間の一番長いもの)とともに入れ替わる。
    /// </summary>
    public static BuffDebuffCardContent Create(
        PlayerBuffSnapshot snapshot,
        string targetName,
        int level,
        string? formatString)
    {
        var entry = new PlayerBuffEntry(snapshot);
        var group = CombatDataCatalog.GetBuffGroup(snapshot.BaseId);
        var name = group != BuffGroup.None
            ? CombatDataCatalog.AppendBuffInternalId(GetGroupName(group), snapshot.BaseId)
            : snapshot.Name;

        return new BuffDebuffCardContent(
            BuffInfoFormatFormatter.Format(name, targetName, GetCasterName(snapshot.Caster), level, formatString),
            entry.DurationWithUnitText,
            entry.LayerText,
            entry.IconPath,
            entry.IconMask,
            entry.IsModuleEffectIcon,
            entry.IsFactorIcon,
            entry.ShowsUnknownIcon);
    }

    /// <summary>
    /// カードに出すバフ名(内部ID注記なし)。<b>料理・薬剤のバフは、まとまりの名前(料理・薬剤)の1語にする。</b>
    /// 表の名前はそのバフを付けるアイテムの名前で、複数つながると長い。
    /// それ以外は一覧の行と同じ名前(表に名前が無ければ付与元をたどった由来の名前)。
    /// </summary>
    public static string GetCardBuffName(PlayerBuffSnapshot snapshot)
    {
        var group = CombatDataCatalog.GetBuffGroup(snapshot.BaseId);
        return group != BuffGroup.None
            ? GetGroupName(group)
            : snapshot.NameWithoutInternalId;
    }

    /// <summary>
    /// 付与者プレイヤー名(<see cref="PlayerBuffSnapshot.Caster"/>)。付与者がプレイヤーでなければ空。
    /// 名前の規則はタイムライン・被ダメログと同じ(<see cref="PlayerInfoFormatFormatter.GetPartyDisplayName"/>)。
    /// </summary>
    public static string GetCasterName(CombatEventLogParty? caster)
        => caster is not null
            ? PlayerInfoFormatFormatter.GetPartyDisplayName(caster)
            : string.Empty;

    /// <summary>まとまりの名前。タイトルの「バフカード(料理)」の括弧の中と同じ文言。バフ・デバフグループのまとまりの行も同じ名前。</summary>
    public static string GetGroupName(BuffGroup group)
        => LocalizationManager.Instance.GetString($"Widget_BuffDebuffCard_Group_{group}");

    /// <summary>
    /// 失効したあとに残す表示値。<b>名前の行だけを組み、アイコン・残り時間・重ね数は出さない。</b>
    ///
    /// <para>
    /// 出す文字列は生きているときと同じ書式で組み直す。控えた文字列を使い回すと、
    /// 失効中に書式を変えても反映されない。
    /// </para>
    ///
    /// <para>
    /// バフ名が一度も分かっていなければ <see cref="Empty"/>。何も出さないのが正しい。
    /// 付与者プレイヤー名は、同じ対象で最後に分かった付与者(呼び出し側が控える)。名前はバフ名と同じく、ここで規則を通して組み直す。
    /// </para>
    /// </summary>
    public static BuffDebuffCardContent CreateNameOnly(
        string lastKnownBuffName,
        string targetName,
        int level,
        string? formatString,
        CombatEventLogParty? lastKnownCaster)
    {
        if (string.IsNullOrWhiteSpace(lastKnownBuffName))
        {
            return Empty;
        }

        return new BuffDebuffCardContent(
            BuffInfoFormatFormatter.Format(lastKnownBuffName, targetName, GetCasterName(lastKnownCaster), level, formatString),
            string.Empty,
            string.Empty,
            null,
            null,
            false,
            false,
            false);
    }

    /// <summary>
    /// カードが追うバフの、設定の行(グループの行・ショートカット・通知対象)の鍵。まとまりを追うカードはまとまり、個別のバフのカードはカードの鍵の番号。
    /// 追うバフが決まっていなければ null。
    /// </summary>
    public static BuffEntryKey? CreateEntryKey(BuffGroup group, string? buffKey)
    {
        if (group != BuffGroup.None)
        {
            return BuffEntryKey.ForGroup(group);
        }

        return MeterSnapshotProvider.TryGetBuffIdFromCardKey(buffKey, out var buffId)
            ? BuffEntryKey.ForBuffDebuffGroup(buffId)
            : null;
    }

    /// <summary>
    /// 右クリックの項目「このバフを通知対象にする」「このバフを通知対象にしない」(デバフは「このデバフを」)の文言。
    /// バフ・デバフカードとバフ・デバフグループで共通。
    /// </summary>
    public static string GetNotificationTargetMenuText(bool isDebuff, bool isNotificationTarget)
    {
        var key = (isDebuff, isNotificationTarget) switch
        {
            (false, false) => "BuffList_Menu_AddBuffToNotificationTargets",
            (false, true) => "BuffList_Menu_RemoveBuffFromNotificationTargets",
            (true, false) => "BuffList_Menu_AddDebuffToNotificationTargets",
            (true, true) => "BuffList_Menu_RemoveDebuffFromNotificationTargets"
        };
        return LocalizationManager.Instance.GetString(key);
    }

    /// <summary>
    /// 右クリックの項目「このバフを優先表示」「このバフの優先表示をやめる」(デバフは「このデバフを」「このデバフの」)の文言。
    /// バフ・デバフ一覧とエンティティ情報で共通。
    /// </summary>
    public static string GetPriorityMenuText(bool isDebuff, bool isPrioritized)
    {
        var key = (isDebuff, isPrioritized) switch
        {
            (false, false) => "BuffList_Menu_PrioritizeBuff",
            (false, true) => "BuffList_Menu_UnprioritizeBuff",
            (true, false) => "BuffList_Menu_PrioritizeDebuff",
            (true, true) => "BuffList_Menu_UnprioritizeDebuff"
        };
        return LocalizationManager.Instance.GetString(key);
    }

    /// <summary>
    /// 倍率の保存キー。まとまりを追う窓は <c>{対象ID}:group:{まとまり}</c>、
    /// 個別のバフを追う窓は <c>{対象ID}:{バフキー}</c>。
    ///
    /// <para>
    /// まとまりで個別のバフキーを使わないのは、料理や薬剤が<b>食べ直すたびに別のIDへ入れ替わる</b>
    /// ため。個別キーのままだと入れ替わりのたびに倍率が既定へ戻ってしまう。
    /// </para>
    ///
    /// <para>
    /// 必要な値が欠けている間は空を返し、呼び出し側が保存も読み出しもしない。
    /// </para>
    /// </summary>
    public static string CreateScaleKey(long targetId, BuffGroup group, string? buffKey)
    {
        if (targetId == 0)
        {
            return string.Empty;
        }

        if (group != BuffGroup.None)
        {
            return $"{targetId}:group:{group}";
        }

        return string.IsNullOrWhiteSpace(buffKey)
            ? string.Empty
            : $"{targetId}:{buffKey}";
    }

    /// <summary>
    /// そのまとまりに属するバフを探す。入れ替わっても同じまとまりなら拾い直せる。
    /// 複数付いていることはゲーム仕様上ないが、あれば一覧順の先頭を採る。
    /// 同じバフの行が複数(エンティティの付与したプレイヤーごと)あるときは、カードに出す行(<see cref="PlayerBuffSnapshot.IsCardRepresentative"/>)だけを見る。
    /// </summary>
    public static PlayerBuffSnapshot? FindByGroup(
        IReadOnlyList<PlayerBuffSnapshot> snapshots,
        BuffGroup group)
    {
        if (group == BuffGroup.None)
        {
            return null;
        }

        foreach (var snapshot in snapshots)
        {
            if (snapshot.IsCardRepresentative
                && CombatDataCatalog.GetBuffGroup(snapshot.BaseId) == group)
            {
                return snapshot;
            }
        }

        return null;
    }

    /// <summary>
    /// タイトルの前半。「バフカード(名前)」「デバフカード(名前)」の形にする。
    ///
    /// <para>
    /// まとまりを追う窓は個別のバフ名ではなく<b>まとまりの名前</b>(料理・薬剤)を出す。
    /// まとまりは対象が居なくても名乗れるので、起動直後にウィジェット名のままになることがない。
    /// </para>
    ///
    /// <para>
    /// 個別のバフを追う窓では、失効しても<b>最後に分かった名前を出し続ける</b>(呼び出し側が保持する)。
    /// 一度も分かっていないときだけウィジェット名に落ちる。
    /// </para>
    /// </summary>
    public static string CreateHeaderPrefix(
        string widgetDisplayName,
        PlayerBuffListKind kind,
        BuffGroup group,
        string lastKnownBuffName)
    {
        var name = group != BuffGroup.None
            ? GetGroupName(group)
            : lastKnownBuffName;

        if (string.IsNullOrWhiteSpace(name))
        {
            return widgetDisplayName;
        }

        return LocalizationManager.Instance.Format(
            kind == PlayerBuffListKind.Debuff
                ? "Widget_BuffDebuffCard_TitleDebuff"
                : "Widget_BuffDebuffCard_TitleBuff",
            name);
    }

    /// <summary>
    /// カードの鍵(<see cref="PlayerBuffSnapshot.CardKey"/>、バフの番号ごと)で行を探す。
    /// 同じ鍵の行が並ぶ(エンティティの付与したプレイヤーごと)ときは、カードに出す行(<see cref="PlayerBuffSnapshot.IsCardRepresentative"/>)。
    /// </summary>
    public static PlayerBuffSnapshot? FindByKey(
        IReadOnlyList<PlayerBuffSnapshot> snapshots,
        string buffKey)
    {
        foreach (var snapshot in snapshots)
        {
            if (snapshot.IsCardRepresentative
                && string.Equals(snapshot.CardKey, buffKey, StringComparison.Ordinal))
            {
                return snapshot;
            }
        }

        return null;
    }
}
