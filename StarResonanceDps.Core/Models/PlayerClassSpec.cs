// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Models;

public enum PlayerClassSpec
{
    Unknown = 0,
    ShieldKnightRecovery,
    ShieldKnightShield,
    HeavyGuardianEarthfort,
    HeavyGuardianBlock,
    StormbladeIaidoSlash,
    StormbladeMoonStrike,
    WindKnightVanGuard,
    WindKnightSkyward,
    FrostMageIcicle,
    FrostMageFrostBeam,
    MarksmanWildpack,
    MarksmanFalconry,
    VerdantOracleSmite,
    VerdantOracleLifeBind,
    SoulMusicianDissonance,
    SoulMusicianConcerto,
    FlameBerserkerFormless,
    FlameBerserkerCrimson,

    /// <summary>
    /// 特化アビリティ未装着。ゲーム内の呼称は「クラスR1」(TalentStage 0)。
    ///
    /// <para>
    /// <see cref="Unknown"/> とは別物。Unknown は「まだ観測できていない」で、
    /// これは「全バフスナップショットを受け取った上でマーカーが無かった」= 確定した未装着。
    /// </para>
    /// </summary>
    Rank1,

    /// <summary>
    /// 変身クラス。職業ID 8 / 14 / 15 は特化(クラスR2)を持たないので、
    /// <c>SubProfessionId</c> ではなく職業IDから固定で決まる。
    ///
    /// <para>
    /// 根拠は <c>TalentStageTable</c> — この3クラスの行は全て <c>RootId=0</c> の「クラスR1」で、
    /// R2 のエントリが存在しない。名前は <c>ProfessionTable</c> に無く、
    /// <c>SkillAoyiTable.ResonanceObject</c> から引く。
    /// </para>
    /// </summary>
    TransformDorothy,
    TransformLucy,
    TransformNatsu,

    /// <summary>
    /// イマジン「絶技！ミーンに変身」(技 3941)で変身している間。
    ///
    /// <para>
    /// この変身では職業IDが値なし(0)で届き、変身クラスの職業IDが無い。
    /// 変身のバフ <see cref="PlayerClassSpecResolver.MeanTransformBuffId"/> が乗っている間だけこれになる。
    /// </para>
    /// </summary>
    TransformMean,

    /// <summary>
    /// クエスト「ゴーレム鋼拳戦」(ダンジョン 8002)でゴーレムに変身している間。
    ///
    /// <para>
    /// ミーンと同じく職業IDが値なし(0)で届く。変身のバフ
    /// <see cref="PlayerClassSpecResolver.GolemTransformBuffId"/> が乗っている間だけこれになる。
    /// </para>
    /// </summary>
    TransformGolem
}

public static class PlayerClassSpecResolver
{
    /// <summary>ミーンに変身している間だけ乗るバフ(变异蜂-变身)。</summary>
    public const int MeanTransformBuffId = 2110101;

    /// <summary>
    /// ゴーレムに変身している間だけ乗るバフ(狩猎怪物首领-变身近战机甲)。
    ///
    /// <para>
    /// 同時に付く 960207(机甲本-已驾驶机甲标记)は搭乗中の印で、変身そのものはこちら。
    /// <b>持続なしで除去も届かない</b>が、クエストはこのバフが乗ったまま進み、
    /// 終わると強制のマップ移動が入って <c>ActiveBuffStore</c> ごと消える。
    /// </para>
    /// </summary>
    public const int GolemTransformBuffId = 960200;

    /// <summary>その実体にミーンの変身のバフが今乗っているか。外れるか時間切れになれば false。</summary>
    public static bool HasMeanTransformBuff(long entityUuid)
        => Services.ActiveBuffStore.Instance.HasActive(entityUuid, MeanTransformBuffId);

    /// <summary>その実体にゴーレムの変身のバフが今乗っているか。マップ移動で消えれば false。</summary>
    public static bool HasGolemTransformBuff(long entityUuid)
        => Services.ActiveBuffStore.Instance.HasActive(entityUuid, GolemTransformBuffId);

    /// <summary>その実体の今のバフで決まる変身(ミーン・ゴーレム)。どちらのバフも乗っていなければ null。</summary>
    public static PlayerClassSpec? GetBuffTransformation(long entityUuid)
    {
        if (HasMeanTransformBuff(entityUuid))
        {
            return PlayerClassSpec.TransformMean;
        }

        return HasGolemTransformBuff(entityUuid) ? PlayerClassSpec.TransformGolem : null;
    }

    /// <summary>変身している間の特化か(アイコンは変身のものにそろえる)。</summary>
    public static bool IsTransformation(PlayerClassSpec spec)
    {
        return spec is PlayerClassSpec.TransformDorothy
            or PlayerClassSpec.TransformLucy
            or PlayerClassSpec.TransformNatsu
            or PlayerClassSpec.TransformMean
            or PlayerClassSpec.TransformGolem;
    }

    /// <summary>
    /// 表示用の特化を解決する。特化の判定は Core で完結させ、ウィジェット側で組み立てない。
    ///
    /// <para>
    /// <see cref="PlayerClassSpec.Rank1"/>(アビリティ未装着)は <c>SubProfessionId</c> だけからは
    /// 導けない。0 は「未装着」と「まだ観測していない」の両方を意味するため、
    /// 未装着が確定したかどうかのフラグと組にして初めて区別できる。
    /// </para>
    /// </summary>
    /// <param name="buffTransformation">
    /// バフで決まる変身(今のバフ <see cref="GetBuffTransformation"/> か、保存した回の記録)。ミーンとゴーレム以外の値と null は変身していない。
    /// </param>
    public static PlayerClassSpec Resolve(
        int professionId,
        int subProfessionId,
        bool isSpecAbilityUnequipped,
        PlayerClassSpec? buffTransformation)
    {
        // ミーンとゴーレムの変身は職業IDを持たないので、バフで決める。
        if (buffTransformation is PlayerClassSpec.TransformMean or PlayerClassSpec.TransformGolem)
        {
            return buffTransformation.Value;
        }

        // 変身クラスは特化を持たない。職業IDだけで決まり、SubProfessionId は見ない。
        if (TryResolveTransformation(professionId, out var transformation))
        {
            return transformation;
        }

        if (subProfessionId > 0)
        {
            return FromSubProfessionId(subProfessionId);
        }

        return isSpecAbilityUnequipped ? PlayerClassSpec.Rank1 : PlayerClassSpec.Unknown;
    }

    /// <summary>
    /// 変身クラス(特化を持たない職業)かどうか。
    /// <see cref="PlayerClassSpec.TransformDorothy"/> のコメントに一次データの根拠がある。
    /// </summary>
    public static bool TryResolveTransformation(int professionId, out PlayerClassSpec spec)
    {
        switch (professionId)
        {
            case 8:
                spec = PlayerClassSpec.TransformDorothy;
                return true;
            case 14:
                spec = PlayerClassSpec.TransformLucy;
                return true;
            case 15:
                spec = PlayerClassSpec.TransformNatsu;
                return true;
            default:
                spec = PlayerClassSpec.Unknown;
                return false;
        }
    }

    public static PlayerClassSpec FromSubProfessionId(int subProfessionId)
    {
        return subProfessionId switch
        {
            01_00_01 => PlayerClassSpec.StormbladeIaidoSlash,
            01_00_02 => PlayerClassSpec.StormbladeMoonStrike,
            02_00_01 => PlayerClassSpec.FrostMageIcicle,
            02_00_02 => PlayerClassSpec.FrostMageFrostBeam,
            03_00_01 => PlayerClassSpec.FlameBerserkerFormless,
            03_00_02 => PlayerClassSpec.FlameBerserkerCrimson,
            04_00_01 => PlayerClassSpec.WindKnightVanGuard,
            04_00_02 => PlayerClassSpec.WindKnightSkyward,
            05_00_01 => PlayerClassSpec.VerdantOracleSmite,
            05_00_02 => PlayerClassSpec.VerdantOracleLifeBind,
            09_00_01 => PlayerClassSpec.HeavyGuardianEarthfort,
            09_00_02 => PlayerClassSpec.HeavyGuardianBlock,
            11_00_01 => PlayerClassSpec.MarksmanWildpack,
            11_00_02 => PlayerClassSpec.MarksmanFalconry,
            12_00_01 => PlayerClassSpec.ShieldKnightRecovery,
            12_00_02 => PlayerClassSpec.ShieldKnightShield,
            13_00_01 => PlayerClassSpec.SoulMusicianDissonance,
            13_00_02 => PlayerClassSpec.SoulMusicianConcerto,
            _ => PlayerClassSpec.Unknown
        };
    }

    /// <summary><see cref="ToSubProfessionId"/> の表。特化の番号の列挙を全部 <see cref="FromSubProfessionId"/> に通して作る。</summary>
    private static readonly Dictionary<PlayerClassSpec, int> SubProfessionIdBySpec =
        Enum.GetValues<CombatRuntime.DataTypes.Enums.Professions.SubProfessionId>()
            .Select(subProfessionId => (int)subProfessionId)
            .Where(subProfessionId => FromSubProfessionId(subProfessionId) != PlayerClassSpec.Unknown)
            .ToDictionary(FromSubProfessionId, subProfessionId => subProfessionId);

    /// <summary>特化の番号(<c>SubProfessionId</c>)。特化(クラスR2)でなければ 0。</summary>
    public static int ToSubProfessionId(PlayerClassSpec spec)
    {
        return SubProfessionIdBySpec.TryGetValue(spec, out var subProfessionId) ? subProfessionId : 0;
    }
}
