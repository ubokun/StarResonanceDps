// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;

namespace StarResonanceDps.Core.Diagnostics;

/// <summary>
/// 特化判定で「独立した2つの根拠が食い違った」回を残す常設の計測。
///
/// <para>
/// 特化は2つの経路で決まる — タレント由来のバフと、置換後スキルID。
/// どちらもマーカーバフ(ツリーの根)という第三の根拠と突き合わせられる。
/// 比べるのは同じ職の2特化のあいだだけ(今の職の特化でないものは <c>Entity.AcceptsSubProfessionId</c> が手前で捨てる)。
/// <b>食い違ったらまず判定表か丸めの誤りを疑う</b>。放置すると別人の特化が表示される。
/// </para>
///
/// <list type="bullet">
///   <item>
///     <b>SpecTalentConflict</b> — 枝タレントのバフが特化Xを書こうとしたが、控えてあるマーカーはY。
///     「専用バフの下流の効果が、共有タレントや反対側の特化からも出る」形が典型で、
///     10刻み丸めが特化を誤って断定している。
///   </item>
///   <item>
///     <b>SpecSkillConflict</b> — 置換後スキルIDが特化Xを示したが、控えてあるマーカーはY。
///     味方に発生させるスキルが判定表に当たるとこの形で出る。
///   </item>
/// </list>
///
/// <para>
/// <b><c>sourceConfigId</c> を必ず残すこと。</b> バフの親は <c>BuffTable</c> にも
/// <c>TalentTable</c> にも書かれておらず、ワイヤの <c>FightSourceInfo</c> にしか無い。
/// これが無いと原因を追えない(名前の一致で追うと間違える)。
/// </para>
///
/// <para>
/// <b>これは調査用の一時計測ではない。</b> 常設で、プローブ撤去の対象外。
/// 検知はフィードバックタブの一覧(<see cref="ResidentDetectionStore"/>)へ送り、報告の本文になる。本文は公開されるので、術者や保持者の番号は持たない。
/// 同じ番号は1起動に1回だけ送る(一覧も種類と番号ごとに1件しか持たない)。
/// </para>
/// </summary>
public static class SpecConflictProbe
{
    private static readonly object Sync = new();
    private static readonly HashSet<(char Kind, int Id)> SeenIds = [];

    /// <summary>
    /// タレント由来のバフが、控えてあるマーカーと違う特化を書こうとした回。
    /// </summary>
    /// <param name="observedBuffId">実際に届いたバフID(丸める前)。</param>
    /// <param name="grantedBuffId">10刻みに丸めた後の、判定表のキー。</param>
    /// <param name="sourceConfigId">
    /// そのバフの親(<c>FightSourceInfo.SourceConfigId</c>)。<b>正体を割り出す唯一の手掛かり。</b>
    /// </param>
    public static void CaptureTalentConflict(
        int observedBuffId,
        int grantedBuffId,
        string spec,
        int distanceFromRoot,
        int fightSourceType,
        int sourceConfigId,
        int markerBuffId,
        string markerSpec)
    {
        if (!ShouldRecord('T', observedBuffId))
        {
            return;
        }

        var id = observedBuffId.ToString(CultureInfo.InvariantCulture);
        var details = string.Create(
            CultureInfo.InvariantCulture,
            $"rounded={grantedBuffId} spec={spec} distance={distanceFromRoot} ftype={fightSourceType} src={sourceConfigId} marker={markerBuffId}({markerSpec})");
        ResidentDetectionStore.Add(ResidentDetectionKind.SpecTalentConflict, id, details);
    }

    /// <summary>
    /// 置換後スキルIDが、控えてあるマーカーと違う特化を示した回。
    /// </summary>
    public static void CaptureReplacedSkillConflict(
        int skillId,
        string spec,
        int markerBuffId,
        string markerSpec)
    {
        if (!ShouldRecord('S', skillId))
        {
            return;
        }

        var id = skillId.ToString(CultureInfo.InvariantCulture);
        var details = string.Create(CultureInfo.InvariantCulture, $"spec={spec} marker={markerBuffId}({markerSpec})");
        ResidentDetectionStore.Add(ResidentDetectionKind.SpecSkillConflict, id, details);
    }

    private static bool ShouldRecord(char kind, int id)
    {
        lock (Sync)
        {
            return SeenIds.Add((kind, id));
        }
    }
}
