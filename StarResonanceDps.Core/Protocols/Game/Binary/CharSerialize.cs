// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class CharSerialize(BlobReader blob) : BlobType(ref blob)
{
    public override string? DebugName => "CharSerialize";

    public int? CharId;
    public CharBaseInfo? CharBaseInfo;
    public SceneData? SceneData;
    public BuffDBInfo? BuffInfo;
    public UserFightAttr? Attr;
    public SeasonRankList? SeasonRankList;
    public ProfessionList? ProfessionList;
    public CurrentProfessionProjectIdInfo? CurrentProjectIdInfo;
    public DutyList? DutyList;
    public FightPoint? FightPoint;
    public SeasonRoleLevelData? SeasonRoleLevelData;
    public ItemPackage? ItemPackage;
    public EquipList? EquipList;
    public Mod? Mod;
    public SeasonCultivateLineData? SeasonCultivateLineData;

    /// <summary>読み損ねを一度だけログに出した項目。</summary>
    private static readonly HashSet<int> ReportedReadFailures = [];

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.CharSerialize.CharIdFieldNumber:
                CharId = blob.ReadInt();
                return true;
            case Zproto.CharSerialize.CharBaseFieldNumber:
                CharBaseInfo = new(blob);
                return true;
            case Zproto.CharSerialize.SceneDataFieldNumber:
                SceneData = new(blob);
                return true;
            case Zproto.CharSerialize.BuffInfoFieldNumber:
                ReadBuffInfoWithoutDisturbingStream(blob);
                return true;
            case Zproto.CharSerialize.AttrFieldNumber:
                Attr = new(blob);
                return true;
            case Zproto.CharSerialize.SeasonRankListFieldNumber:
                SeasonRankList = new(blob);
                return true;
            case Zproto.CharSerialize.ProfessionListFieldNumber:
                ProfessionList = new(blob);
                return true;
            case Zproto.CharSerialize.CurProjectIdInfoFieldNumber:
                CurrentProjectIdInfo = new(blob);
                return true;
            case Zproto.CharSerialize.DutyListFieldNumber:
                DutyList = new(blob);
                return true;
            case Zproto.CharSerialize.FightPointFieldNumber:
                FightPoint = new(blob);
                return true;
            case Zproto.CharSerialize.SeasonRoleLevelDataFieldNumber:
                SeasonRoleLevelData = new(blob);
                return true;
            case Zproto.CharSerialize.SaveSerialFieldNumber:
                // 保存の通し番号。使い先は無いが、数値は長さが分からず読み飛ばせないので読んで捨てる
                // (読まないと、この後ろの項目(106 今のプロジェクト・107 ロールスキルの一覧 など)をその差分で失う)。
                blob.ReadLong();
                return true;
            case Zproto.CharSerialize.ItemPackageFieldNumber:
                return TryReadWithoutDisturbingStream(blob, index, reader => ItemPackage = new(reader));
            case Zproto.CharSerialize.EquipFieldNumber:
                return TryReadWithoutDisturbingStream(blob, index, reader => EquipList = new(reader));
            case Zproto.CharSerialize.ModFieldNumber:
                return TryReadWithoutDisturbingStream(blob, index, reader => Mod = new(reader));
            case Zproto.CharSerialize.SeasonCultivateLineDataFieldNumber:
                return TryReadWithoutDisturbingStream(blob, index, reader => SeasonCultivateLineData = new(reader));
            default:
                return false;
        }
    }

    /// <summary>
    /// 入れ子の項目を読む。<b>外側のストリーム位置は、読み飛ばして確定させた終端で必ず終える</b>
    /// (<see cref="ReadBuffInfoWithoutDisturbingStream"/> と同じ考え方。解釈がずれても後続の項目を失わない)。
    /// 読み飛ばせない形なら <c>false</c> を返して、ほかの未対応の項目と同じ扱いにする。
    /// 中で例外が出たら、その項目は無いものとして続け、項目ごとに一度だけ警告を出す。
    /// </summary>
    private static bool TryReadWithoutDisturbingStream(BlobReader blob, int index, Action<BlobReader> read)
    {
        var start = blob.Offset;
        if (!blob.TrySkipNestedBlob())
        {
            blob.Offset = start;
            return false;
        }

        var end = blob.Offset;
        try
        {
            blob.Offset = start;
            read(blob);
        }
        catch (Exception ex)
        {
            bool isFirst;
            lock (ReportedReadFailures)
            {
                isFirst = ReportedReadFailures.Add(index);
            }

            if (isFirst)
            {
                Serilog.Log.Warning(
                    ex,
                    "Could not read field {FieldIndex} of the container delta; the change is skipped until the next full container",
                    index);
            }
        }
        finally
        {
            blob.Offset = end;
        }

        return true;
    }

    /// <summary>
    /// buffInfo(field 6)を読む。<b>外側のストリーム位置は必ず従来と同じところで終える。</b>
    ///
    /// <para>
    /// このフィールドは従来 <see cref="BlobReader.TrySkipNestedBlob"/> で読み飛ばされていた。
    /// 素直にパースすると、こちらの解釈が1バイトでもずれた時点で、後続の
    /// professionList(61) や dutyList(107) が丸ごと失われる。
    /// そこで先に読み飛ばして正しい終端を確定させ、その範囲だけを読み直し、
    /// 結果によらず終端へ戻す。パースに失敗しても <see cref="BuffInfo"/> が null になるだけで済む。
    /// </para>
    /// </summary>
    private void ReadBuffInfoWithoutDisturbingStream(BlobReader blob)
    {
        var start = blob.Offset;
        if (!blob.TrySkipNestedBlob())
        {
            // 読み飛ばせない形。従来はここで解析打ち切りになっていたので、挙動を変えずに戻す。
            blob.Offset = start;
            return;
        }

        var end = blob.Offset;
        try
        {
            blob.Offset = start;
            BuffInfo = new(blob);
        }
        catch
        {
            BuffInfo = null;
        }
        finally
        {
            blob.Offset = end;
        }
    }
}
