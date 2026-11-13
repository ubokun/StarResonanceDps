// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.Generic;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    /// <summary>
    /// 職業の定義表。<b>使う項目だけ持つ</b>(残りは読み飛ばされる)。
    ///
    /// <para>
    /// ステータス詳細で、職業ごとに出す攻撃力と主ステータスを決めるのに使う。
    /// 直書きの分岐で持つとゲームの表とずれる。
    /// </para>
    ///
    /// <para>
    /// 推移グラフの技のアイコンの枠(<c>CombatDataCatalog.GetSkillIconFrame</c>)にも使う。
    /// 変身クラスの技は技の表の枠の番号を持たないので、職業の技の4列を番号の代わりにする。
    /// </para>
    /// </summary>
    public class ProfessionSystemTable
    {
        /// <summary>鍵は職業ID(表の <c>Id</c> と <c>ProfessionId</c> は全行で一致する)。</summary>
        public Dictionary<string, ProfessionSystem> Data = new();
    }

    public class ProfessionSystem
    {
        public int ProfessionId { get; set; }

        /// <summary>役割のタグ(<c>TalentTagTable</c> の番号。1 攻撃 / 2 支援 / 3 防御)。</summary>
        public int Talent { get; set; }

        /// <summary>
        /// 出す攻撃力の属性番号。<c>[[0, 番号], [1, 番号]]</c> の形で、先頭の値は種別、2つ目が番号。
        /// いまは 2 要素とも同じ番号を指す。
        /// </summary>
        public List<List<int>> AttackShow { get; set; } = null!;

        /// <summary>出す主ステータス(筋力・知力・敏捷)の属性番号。形は <see cref="AttackShow"/> と同じ。</summary>
        public List<List<int>> StrOrIntOrDexShow { get; set; } = null!;

        /// <summary>通常攻撃の技ID。</summary>
        public List<int> NormalAttackSkill { get; set; } = null!;

        /// <summary>特殊攻撃の技ID。</summary>
        public List<int> SpecialSkill { get; set; } = null!;

        /// <summary>究極スキルの技ID。</summary>
        public List<int> UltimateSkill { get; set; } = null!;

        /// <summary>マスタリースキルの技ID。</summary>
        public List<int> NormalSkill { get; set; } = null!;
    }
}
