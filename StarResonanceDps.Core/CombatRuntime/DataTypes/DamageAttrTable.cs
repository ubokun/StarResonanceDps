// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.Frozen;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    /// <summary>
    /// ダメージの表 <c>DamageAttrTable</c>。使うのは1発で敵の強靭(ブレイクゲージ)を削る量 <c>PVEStunnedDamage</c> だけ(ブレイクメーター、notes/206)。
    ///
    /// <para>
    /// 行の番号は被弾の種類で形が違う。技は <c>1</c> ＋ 技ID ＋ 段2桁 ＋ ヒット2桁(段は <c>01</c> を使う)、
    /// バフは <c>2</c> ＋ バフID ＋ ヒット2桁、弾は <c>3</c> ＋ 弾ID ＋ ヒット2桁。値は被弾のレベルの位置(配列が短ければ最後)。
    /// </para>
    /// </summary>
    public class DamageAttrTable
    {
        private FrozenDictionary<long, int[]> _stunnedDamages = FrozenDictionary<long, int[]>.Empty;

        public void Load(Dictionary<string, DamageAttr> table)
        {
            _stunnedDamages = table.Values.ToFrozenDictionary(row => row.Id, row => row.PVEStunnedDamage ?? []);
        }

        /// <summary>
        /// 被弾1発の強靭の削り量を表から引く。行が無ければ false(行があって値が無ければ true で 0)。
        /// </summary>
        public bool TryGetStunnedDamage(EDamageSource source, int ownerId, int ownerLevel, int hitEventId, out int value)
        {
            value = 0;
            long head = source switch
            {
                EDamageSource.Skill => 1,
                EDamageSource.Buff => 2,
                EDamageSource.Bullet or EDamageSource.FakeBullet => 3,
                _ => 0
            };
            if (head == 0 || ownerId <= 0 || hitEventId is < 0 or > 99)
            {
                return false;
            }

            var id = head;
            for (var digits = ownerId; digits > 0; digits /= 10)
            {
                id *= 10;
            }

            id += ownerId;
            id = source == EDamageSource.Skill ? (id * 100 + 1) * 100 + hitEventId : id * 100 + hitEventId;
            if (!_stunnedDamages.TryGetValue(id, out var values))
            {
                return false;
            }

            if (values.Length > 0)
            {
                value = values[Math.Clamp(ownerLevel, 1, values.Length) - 1];
            }

            return true;
        }
    }

    public class DamageAttr
    {
        public long Id { get; set; }
        public int[]? PVEStunnedDamage { get; set; }
    }
}
