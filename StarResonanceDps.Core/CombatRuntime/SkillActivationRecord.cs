// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using ProtoBuf;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// プレイヤーの技の開始1件の DB の形(<c>ExData</c> の項目13)。推移グラフの横軸の下にアイコンで出す。
/// 自分以外は差分で <c>AttrSkillId</c> に技が入ったとき、自分は上りの要求 <c>UseSlot</c> に成功の応答が返ったときに残す。
/// 出現・入場の全属性に入っている技は、その時点で始めたものではないので残さない。
/// 実行中は <see cref="PlayerSkillActivationLog"/> に持ち、保存のときに写す。履歴を読むときに戻す。
/// </summary>
[ProtoContract]
public sealed class SkillActivationRecord
{
    [ProtoMember(1)]
    public long PlayerUuid { get; set; }

    [ProtoMember(2)]
    public int SkillId { get; set; }

    /// <summary>技の開始(自分は成功の応答)が届いたパケットの到着時刻(UTC)。</summary>
    [ProtoMember(3)]
    public DateTime Timestamp { get; set; }
}

/// <summary>読むときの技の開始1件。</summary>
public readonly record struct SkillActivation(int SkillId, DateTime Timestamp);

/// <summary>
/// 1人の技の開始。回のあいだ全部残し、開いている推移グラフがその人の続きから読むだけなので、圧縮して持つ。
/// 時刻はその人の最初の開始からのミリ秒で持つ。排他は持ち主の <see cref="Encounter"/> が持つ。
/// </summary>
/// <param name="firstTicks">その人の最初の開始の到着時刻(<see cref="DateTime.Ticks"/>)。</param>
internal sealed class PlayerSkillActivationLog(long firstTicks)
{
    private readonly CompressedRecordLog<Row> _rows = new();

    public int Count => _rows.Count;

    public void Add(int skillId, DateTime arrivalUtc)
    {
        _rows.Add(new Row(skillId, checked((int)((arrivalUtc.Ticks - firstTicks) / TimeSpan.TicksPerMillisecond))));
    }

    /// <summary><paramref name="startIndex"/> 番目から後の全部。無ければ空。</summary>
    public SkillActivation[] ReadFrom(int startIndex)
    {
        var rows = _rows.ToArray(startIndex);
        if (rows.Length == 0)
        {
            return [];
        }

        var activations = new SkillActivation[rows.Length];
        for (var index = 0; index < rows.Length; index++)
        {
            activations[index] = new SkillActivation(rows[index].SkillId, ToUtc(rows[index]));
        }

        return activations;
    }

    /// <summary>全部を DB の形にして <paramref name="records"/> へ足す。</summary>
    public void AppendRecords(long playerUuid, List<SkillActivationRecord> records)
    {
        foreach (var row in _rows.ToArray(0))
        {
            records.Add(new SkillActivationRecord
            {
                PlayerUuid = playerUuid,
                SkillId = row.SkillId,
                Timestamp = ToUtc(row),
            });
        }
    }

    private DateTime ToUtc(Row row)
    {
        return new DateTime(firstTicks + row.OffsetMilliseconds * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    }

    private readonly record struct Row(int SkillId, int OffsetMilliseconds);
}
