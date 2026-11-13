// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// ダメージ・回復イベントの発生源を、メーターの行に使うキーへ畳む。
///
/// <para>
/// イベントの <c>OwnerId</c> は <c>DamageSource</c> によって中身が変わる。
/// スキルID・弾ID・バフIDのどれかで、そのままではメーターの行にならない。
/// </para>
///
/// <list type="number">
///   <item>発生源キー — <c>OwnerId</c> と <c>HitEventId</c> の組。ゲームの行はこの粒度で決まる</item>
///   <item>行対応表 — 同じ行に属するキーを、その行の代表キーへ寄せる</item>
/// </list>
///
/// <para>
/// <b>畳み先を決めているのはゲーム内メーターの見出し表(<c>RecountTable</c>)だけ。</b>
/// ゲームは <c>RecountTable.DamageId</c> で行を引いており、<c>DamageId</c> の <c>TypeEnum</c> が
/// <c>OwnerId</c>、下2桁が <c>HitEventId</c> にあたる。表に無いキーは畳まず、そのまま出す。
/// </para>
///
/// <para>
/// <b><c>OwnerId</c> だけを鍵にしてはいけない。</b> 同じ <c>OwnerId</c> の枝が別の行に入る例が
/// 26〜28種あり、枝番を見ないと片方の行が消える。
/// </para>
///
/// <para>
/// <b>実行時の情報からは畳まない。</b> バフ実体の <c>FightSourceInfo</c>・コンボの
/// <c>NextSkillId</c>・召喚体の <c>AttrId</c>・ダメージ属性の式は、
/// どれもゲーム内の行と食い違う。見出し表だけを使うこと。
/// </para>
/// </summary>
public static class SkillSourceResolver
{
    /// <summary>畳んだ結果。</summary>
    /// <param name="Key">
    /// メーターの行に使うキー。<c>CombatDataCatalog.FormatSourceKey</c> で <c>ownerId:枝番</c> に戻る。
    /// </param>
    /// <param name="IsBuffSource">
    /// true ならバフとして届いた発生源。<b>名前には影響しない</b>(見出し表は種別を区別しない)。
    /// 内部ID注記の表示区分にだけ効く。
    /// </param>
    public readonly record struct Result(long Key, bool IsBuffSource);

    /// <summary>発生源を畳む。</summary>
    public static Result Resolve(EDamageSource damageSource, int ownerId, int hitEventId)
    {
        var isBuff = damageSource == EDamageSource.Buff;
        if (ownerId == 0)
        {
            return new Result(0, isBuff);
        }

        var key = CombatDataCatalog.MakeSourceKey(ownerId, hitEventId);

        // 同じ行に属するキーを行代表へ寄せる。ゲーム内メーターの行構成そのもの。
        if (CombatDataCatalog.TryResolveRecountRow(key, out var rowKey))
        {
            return new Result(rowKey, isBuff);
        }

        // 表に無いキーは畳まずそのまま出す。表示は空文字になるが、
        // 内部ID注記を出せば ownerId:枝番 が見えるので、表に足りていないことが分かる。
        return new Result(key, isBuff);
    }
}
